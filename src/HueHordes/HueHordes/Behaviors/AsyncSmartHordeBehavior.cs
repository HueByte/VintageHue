using System;
using System.Threading;
using System.Threading.Tasks;
using HueHordes.AI;
using HueHordes.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.Behaviors;

/// <summary>
/// Asynchronous version of smart horde behavior with non-blocking operations
/// </summary>
public class AsyncSmartHordeBehavior : EntityBehavior
{
    private AsyncSmartTargeting? targetingSystem;
    private AsyncBaseDetection? baseDetection;
    private ICoreServerAPI? sapi;

    private HordeTarget? currentTarget;
    private string originalPlayerUID = string.Empty;
    private float behaviorDuration;
    private float timeLeft;
    private float speed = 0.07f;
    private float targetUpdateInterval = 1.0f;
    private float timeSinceLastUpdate;

    private Vec3d? lastKnownTargetPosition;
    private float lostTargetTime;
    private const float MAX_LOST_TARGET_TIME = 10.0f;

    // Patrol behavior
    private bool isPatrolling = false;
    private Vec3d? patrolCenter;
    private float patrolRadius = 15f;
    private float patrolWaitTime = 0f;

    // Async operation management
    private Task? currentAsyncOperation;
    private CancellationTokenSource cancellationTokenSource = new();
    private readonly SemaphoreSlim updateSemaphore = new(1, 1); // Prevent concurrent updates

    public AsyncSmartHordeBehavior(Entity entity) : base(entity)
    {
        if (entity.Api is ICoreServerAPI serverApi)
        {
            sapi = serverApi;

            // Get systems from the singleton AsyncHordeAI instance
            var hordeAI = AsyncHordeAI.Instance;
            if (hordeAI != null)
            {
                baseDetection = hordeAI.BaseDetection;
                targetingSystem = hordeAI.SmartTargeting;
            }
            else
            {
                // Fallback to new instances if singleton not available
                baseDetection = new AsyncBaseDetection(serverApi);
                targetingSystem = new AsyncSmartTargeting(serverApi, baseDetection);
            }
        }

        // Initialize from watched attributes
        if (entity.WatchedAttributes != null)
        {
            originalPlayerUID = entity.WatchedAttributes.GetString("hordeTargetPlayerUID", "");
            behaviorDuration = entity.WatchedAttributes.GetFloat("hordeBehaviorDuration", 60f);
            speed = entity.WatchedAttributes.GetFloat("hordeSpeed", 0.07f);

            double x = entity.WatchedAttributes.GetDouble("hordeOriginalTargetX", 0);
            double y = entity.WatchedAttributes.GetDouble("hordeOriginalTargetY", 0);
            double z = entity.WatchedAttributes.GetDouble("hordeOriginalTargetZ", 0);
            if (x != 0 || y != 0 || z != 0)
            {
                lastKnownTargetPosition = new Vec3d(x, y, z);
            }
        }
        else
        {
            // Fallback values
            behaviorDuration = 60f;
            speed = 0.07f;
        }

        timeLeft = behaviorDuration;
        timeSinceLastUpdate = 0f;
    }

    public override void OnGameTick(float dt)
    {
        if (timeLeft <= 0f || entity?.ServerPos == null || sapi == null)
        {
            CleanupAsync();
            entity?.RemoveBehavior(this);
            return;
        }

        timeLeft -= dt;
        timeSinceLastUpdate += dt;

        // Update target periodically (asynchronously)
        if (timeSinceLastUpdate >= targetUpdateInterval &&
            (currentAsyncOperation?.IsCompleted ?? true))
        {
            currentAsyncOperation = UpdateTargetAsync();
            timeSinceLastUpdate = 0f;
        }

        // Execute behavior based on current state (synchronously for game tick)
        if (currentTarget != null && currentTarget.IsValid(sapi.World.Calendar.TotalDays))
        {
            ExecuteTargetBehavior(dt);
        }
        else if (isPatrolling)
        {
            ExecutePatrolBehavior(dt);
        }
        else
        {
            // No target and not patrolling - try to initialize patrol
            InitializePatrol();
        }
    }

    /// <summary>
    /// Asynchronously update current target using the smart targeting system
    /// </summary>
    private async Task UpdateTargetAsync()
    {
        if (targetingSystem == null || entity?.ServerPos == null) return;

        // Use semaphore to prevent concurrent async updates
        if (!await updateSemaphore.WaitAsync(100, cancellationTokenSource.Token))
            return; // Skip this update if another is in progress

        try
        {
            var currentPos = entity.ServerPos.XYZ;

            // If we have a current target, check if it's still valid
            if (currentTarget != null)
            {
                await targetingSystem.UpdateTargetAsync(currentTarget, cancellationTokenSource.Token);

                if (!currentTarget.IsValid(sapi!.World.Calendar.TotalDays))
                {
                    // Target lost - record position and try to find fallback
                    lastKnownTargetPosition = currentTarget.Position.Clone();
                    lostTargetTime = 0f;

                    var fallback = await targetingSystem.GetFallbackTargetAsync(
                        lastKnownTargetPosition, originalPlayerUID, currentTarget, cancellationTokenSource.Token);
                    currentTarget = fallback;

                    if (currentTarget == null)
                    {
                        InitializePatrol();
                        return;
                    }
                }
            }

            // If no current target, or periodically refresh to find better targets
            if (currentTarget == null || ShouldSeekBetterTarget())
            {
                var newTarget = await targetingSystem.GetBestTargetAsync(
                    currentPos, originalPlayerUID, 100.0, cancellationTokenSource.Token);

                if (newTarget != null && (currentTarget == null || newTarget.Priority > currentTarget.Priority + 10))
                {
                    currentTarget = newTarget;
                    isPatrolling = false; // Stop patrolling if we found a target
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful cancellation handling
        }
        catch (Exception ex)
        {
            sapi?.Logger.Warning($"[AsyncSmartHordeBehavior] Error updating target: {ex.Message}");
        }
        finally
        {
            updateSemaphore.Release();
        }
    }

    /// <summary>
    /// Execute behavior when we have an active target (synchronous for game tick)
    /// </summary>
    private void ExecuteTargetBehavior(float dt)
    {
        if (currentTarget == null || entity?.ServerPos == null) return;

        var currentPos = entity.ServerPos.XYZ;
        var targetPos = currentTarget.Position;
        var distanceToTarget = Math.Sqrt(currentPos.SquareDistanceTo(targetPos));

        // If we're very close to target, reduce speed or stop
        if (distanceToTarget < 2.0)
        {
            HandleCloseToTarget(dt);
            return;
        }

        // Move toward target
        MoveToward(targetPos, dt);

        // Check if we lost line of sight to player targets (async check in background)
        if (currentTarget.Type == TargetType.Player && targetingSystem != null)
        {
            // Start async line-of-sight check without blocking
            _ = CheckLineOfSightAsync(currentPos, targetPos);
        }
    }

    /// <summary>
    /// Asynchronously check line of sight (non-blocking)
    /// </summary>
    private async Task CheckLineOfSightAsync(Vec3d currentPos, Vec3d targetPos)
    {
        try
        {
            if (targetingSystem == null || cancellationTokenSource.Token.IsCancellationRequested) return;

            var hasLOS = await targetingSystem.HasLineOfSightAsync(currentPos, targetPos, 50f, cancellationTokenSource.Token);

            if (!hasLOS)
            {
                lostTargetTime += targetUpdateInterval; // Approximate time increment
                if (lostTargetTime > MAX_LOST_TARGET_TIME)
                {
                    // Lost target for too long, find fallback
                    var fallback = await targetingSystem.GetFallbackTargetAsync(
                        targetPos, originalPlayerUID, currentTarget, cancellationTokenSource.Token);
                    currentTarget = fallback;
                    lostTargetTime = 0f;
                }
            }
            else
            {
                lostTargetTime = 0f; // Reset lost timer
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful cancellation
        }
        catch (Exception ex)
        {
            sapi?.Logger.Debug($"[AsyncSmartHordeBehavior] Line of sight check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Execute patrol behavior when no active targets (synchronous)
    /// </summary>
    private void ExecutePatrolBehavior(float dt)
    {
        if (patrolCenter == null || entity?.ServerPos == null) return;

        var currentPos = entity.ServerPos.XYZ;

        // If we're waiting, just stand still
        if (patrolWaitTime > 0f)
        {
            patrolWaitTime -= dt;
            return;
        }

        // Pick a random point within patrol radius
        var rand = new Random();
        var angle = rand.NextDouble() * Math.PI * 2;
        var distance = rand.NextDouble() * patrolRadius;

        var patrolTarget = new Vec3d(
            patrolCenter.X + Math.Cos(angle) * distance,
            patrolCenter.Y,
            patrolCenter.Z + Math.Sin(angle) * distance
        );

        // Move toward patrol target
        var distanceToPatrol = Math.Sqrt(currentPos.SquareDistanceTo(patrolTarget));
        if (distanceToPatrol > 1.0)
        {
            MoveToward(patrolTarget, dt, speed * 0.5f); // Patrol at half speed
        }
        else
        {
            // Reached patrol point, wait a bit
            patrolWaitTime = 2f + (float)(rand.NextDouble() * 3f); // Wait 2-5 seconds
        }
    }

    /// <summary>
    /// Move entity toward a target position (synchronous for immediate response)
    /// </summary>
    private void MoveToward(Vec3d targetPosition, float dt, float? customSpeed = null)
    {
        if (entity?.ServerPos == null) return;

        var currentPos = entity.ServerPos.XYZ;
        var moveSpeed = customSpeed ?? speed;

        // Calculate movement vector
        var dx = targetPosition.X - currentPos.X;
        var dz = targetPosition.Z - currentPos.Z;
        var horizontalDistance = Math.Max(0.001, Math.Sqrt(dx * dx + dz * dz));

        // Normalize and apply speed
        var vx = (dx / horizontalDistance) * moveSpeed;
        var vz = (dz / horizontalDistance) * moveSpeed;

        // Add some randomness to avoid perfect straight lines
        var rand = new Random();
        vx += (rand.NextDouble() - 0.5) * 0.01;
        vz += (rand.NextDouble() - 0.5) * 0.01;

        // Apply movement
        entity.ServerPos.Motion.Add(vx, 0, vz);
        entity.Pos.SetFrom(entity.ServerPos);
    }

    /// <summary>
    /// Handle behavior when very close to target
    /// </summary>
    private void HandleCloseToTarget(float dt)
    {
        if (currentTarget == null) return;

        switch (currentTarget.Type)
        {
            case TargetType.Player:
                // Continue attacking/following player
                MoveToward(currentTarget.Position, dt, speed * 0.3f);
                break;

            case TargetType.BaseEntrance:
                // At entrance, switch to patrol or look for player
                InitializePatrol();
                break;

            case TargetType.BaseCenter:
                // At base center, start patrolling
                InitializePatrol();
                break;

            case TargetType.PatrolPoint:
                // Reached patrol point, find new one
                currentTarget = null;
                patrolWaitTime = 1f;
                break;
        }
    }

    /// <summary>
    /// Initialize patrol behavior around base center
    /// </summary>
    private void InitializePatrol()
    {
        if (baseDetection == null || string.IsNullOrEmpty(originalPlayerUID)) return;

        var playerBase = baseDetection.GetPlayerBase(originalPlayerUID);
        if (playerBase != null)
        {
            patrolCenter = playerBase.Center.Clone();
            patrolRadius = Math.Max(10f, Math.Min(25f, (playerBase.BoundingBox.MaxX - playerBase.BoundingBox.MinX) / 2));
        }
        else if (lastKnownTargetPosition != null)
        {
            patrolCenter = lastKnownTargetPosition.Clone();
            patrolRadius = 15f;
        }
        else if (entity?.ServerPos != null)
        {
            patrolCenter = entity.ServerPos.XYZ.Clone();
            patrolRadius = 10f;
        }

        isPatrolling = true;
        currentTarget = null;
        patrolWaitTime = 0f;
    }

    /// <summary>
    /// Determine if we should look for a better target
    /// </summary>
    private bool ShouldSeekBetterTarget()
    {
        if (currentTarget == null) return true;

        // Always seek better targets for low-priority ones
        if (currentTarget.Priority < 50) return true;

        // Occasionally check for better targets even when we have good ones
        var rand = new Random();
        return rand.NextDouble() < 0.1; // 10% chance each update
    }

    /// <summary>
    /// Cleanup async operations
    /// </summary>
    private async void CleanupAsync()
    {
        try
        {
            cancellationTokenSource.Cancel();

            // Wait for current operations to complete (with timeout)
            if (currentAsyncOperation != null)
            {
                var timeoutTask = Task.Delay(1000);
                await Task.WhenAny(currentAsyncOperation, timeoutTask);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        finally
        {
            updateSemaphore.Dispose();
            cancellationTokenSource.Dispose();
        }
    }

    public override string PropertyName() => "asyncsmarthorde";
}