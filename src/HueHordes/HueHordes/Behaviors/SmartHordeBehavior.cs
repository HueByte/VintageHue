using System;
using HueHordes.AI;
using HueHordes.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.Behaviors;

/// <summary>
/// Advanced horde behavior that uses smart targeting and dynamic decision making
/// </summary>
public class SmartHordeBehavior : EntityBehavior
{
    private SmartTargeting? targetingSystem;
    private BaseDetection? baseDetection;
    private ICoreServerAPI? sapi;

    private HordeTarget? currentTarget;
    private string originalPlayerUID = string.Empty;
    private float behaviorDuration;
    private float timeLeft;
    private float speed = 0.07f;
    private float targetUpdateInterval = 1.0f; // Update target every second
    private float timeSinceLastUpdate;

    private Vec3d? lastKnownTargetPosition;
    private float lostTargetTime;
    private const float MAX_LOST_TARGET_TIME = 10.0f; // Give up after 10 seconds

    // Patrol behavior
    private bool isPatrolling = false;
    private Vec3d? patrolCenter;
    private float patrolRadius = 15f;
    private float patrolWaitTime = 0f;

    public SmartHordeBehavior(Entity entity) : base(entity)
    {
        if (entity.Api is ICoreServerAPI serverApi)
        {
            sapi = serverApi;

            // Get systems from the singleton HordeAI instance
            var hordeAI = HordeAI.Instance;
            if (hordeAI != null)
            {
                baseDetection = hordeAI.BaseDetection;
                targetingSystem = hordeAI.SmartTargeting;
            }
            else
            {
                // Fallback to new instances if singleton not available
                baseDetection = new BaseDetection(serverApi);
                targetingSystem = new SmartTargeting(serverApi, baseDetection);
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
            entity?.RemoveBehavior(this);
            return;
        }

        timeLeft -= dt;
        timeSinceLastUpdate += dt;

        // Update target periodically
        if (timeSinceLastUpdate >= targetUpdateInterval)
        {
            UpdateTarget();
            timeSinceLastUpdate = 0f;
        }

        // Execute behavior based on current state
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
            // No target and not patrolling - try to find something to do
            InitializePatrol();
        }
    }

    /// <summary>
    /// Update current target using the smart targeting system
    /// </summary>
    private void UpdateTarget()
    {
        if (targetingSystem == null || entity?.ServerPos == null) return;

        var currentPos = entity.ServerPos.XYZ;

        // If we have a current target, check if it's still valid
        if (currentTarget != null)
        {
            targetingSystem.UpdateTarget(currentTarget);

            if (!currentTarget.IsValid(sapi!.World.Calendar.TotalDays))
            {
                // Target lost - record position and try to find fallback
                lastKnownTargetPosition = currentTarget.Position.Clone();
                lostTargetTime = 0f;

                var fallback = targetingSystem.GetFallbackTarget(lastKnownTargetPosition, originalPlayerUID, currentTarget);
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
            var newTarget = targetingSystem.GetBestTarget(currentPos, originalPlayerUID);

            if (newTarget != null && (currentTarget == null || newTarget.Priority > currentTarget.Priority + 10))
            {
                currentTarget = newTarget;
                isPatrolling = false; // Stop patrolling if we found a target
            }
        }
    }

    /// <summary>
    /// Execute behavior when we have an active target
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

        // Check if we lost line of sight to player targets
        if (currentTarget.Type == TargetType.Player && targetingSystem != null)
        {
            if (!targetingSystem.HasLineOfSight(currentPos, targetPos))
            {
                lostTargetTime += dt;
                if (lostTargetTime > MAX_LOST_TARGET_TIME)
                {
                    // Lost target for too long, find fallback
                    var fallback = targetingSystem.GetFallbackTarget(targetPos, originalPlayerUID, currentTarget);
                    currentTarget = fallback;
                    lostTargetTime = 0f;
                }
            }
            else
            {
                lostTargetTime = 0f; // Reset lost timer
            }
        }
    }

    /// <summary>
    /// Execute patrol behavior when no active targets
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
    /// Move entity toward a target position
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

    public override string PropertyName() => "smarthorde";
}