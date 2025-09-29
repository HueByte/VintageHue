using System;
using System.Collections.Generic;
using HueHordes.Debug;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Clean AI behavior that follows the step-by-step logic:
/// 1. Spawn → Navigate to base center
/// 2. Attack players on sight while pathfinding
/// 3. Return to base navigation when player is lost
/// 4. Priority on doors/gates for destruction
/// </summary>
public class AIBehavior : EntityBehavior
{
    private readonly ICoreServerAPI sapi;
    private readonly AStarPathfinder pathfinder;
    private readonly TargetDetection targetDetection;
    private DoorHealthManager? doorHealthManager;
    private static DebugVisualization? debugVisualization;

    // AI State
    private AIState currentState = AIState.NavigatingToBase;
    private Vec3d baseCenter;
    private List<Vec3d> currentPath = new();
    private int currentPathIndex = 0;
    private Entity? currentTarget;
    private long lastTargetSeen;
    private long lastStateChange;
    private long lastJumpTime = 0;

    // Performance and stuck detection
    private int tickCounter = 0;
    private Vec3d lastPosition;
    private long lastMovementTime;
    private bool isStuck = false;

    // Configuration
    private const float TARGET_DETECTION_RANGE = 15f;
    private const float ATTACK_RANGE = 2f;
    private const float PATH_RECALC_INTERVAL = 5000f; // 5 seconds
    private const float TARGET_LOST_TIMEOUT = 3000f; // 3 seconds
    private const float DOOR_PRIORITY_RANGE = 8f;
    private const float JUMP_COOLDOWN = 1000f; // 1 second between jumps
    private const float AI_TIMEOUT = 300000f; // 5 minutes - give up and return to default AI
    private const int PATH_UPDATE_INTERVAL = 5; // Update path every 5 ticks
    private const float STUCK_TIMEOUT = 3000f; // 3 seconds without movement = stuck
    private const float STUCK_MOVEMENT_THRESHOLD = 0.5f; // Minimum movement distance

    private long lastPathCalculation = 0;

    public AIBehavior(Entity entity, DoorHealthManager doorHealthManager) : base(entity)
    {
        sapi = entity.Api as ICoreServerAPI ?? throw new InvalidOperationException("AIBehavior requires server-side API");
        pathfinder = new AStarPathfinder(sapi);
        targetDetection = new TargetDetection(sapi);
        this.doorHealthManager = doorHealthManager ?? throw new ArgumentNullException(nameof(doorHealthManager));

        // Get base center from entity attributes (set by spawning system)
        // Try base target first, then fall back to regular target
        var baseX = entity.WatchedAttributes.GetDouble("baseTargetX",
                      entity.WatchedAttributes.GetDouble("targetX", 0));
        var baseY = entity.WatchedAttributes.GetDouble("baseTargetY",
                      entity.WatchedAttributes.GetDouble("targetY", 0));
        var baseZ = entity.WatchedAttributes.GetDouble("baseTargetZ",
                      entity.WatchedAttributes.GetDouble("targetZ", 0));
        baseCenter = new Vec3d(baseX, baseY, baseZ);

        lastStateChange = sapi.World.ElapsedMilliseconds;
        lastPosition = entity.ServerPos.XYZ.Clone();
        lastMovementTime = lastStateChange;

        DebugLogger.AIEvent("AI initialized",
            $"Entity #{entity.EntityId}, Base: {baseCenter.X:F1},{baseCenter.Y:F1},{baseCenter.Z:F1}",
            entity.EntityId.ToString());
    }

    public override void OnGameTick(float dt)
    {
        if (entity?.ServerPos == null || !entity.Alive) return;

        // Performance optimization - only update every 5 ticks
        tickCounter++;
        if (tickCounter % PATH_UPDATE_INTERVAL != 0)
        {
            // Still execute movement on every tick for smooth motion
            ExecuteMovement(dt);
            return;
        }

        var currentTime = sapi.World.ElapsedMilliseconds;
        var entityPos = entity.ServerPos.XYZ;

        // Check for stuck condition
        CheckAndHandleStuckCondition(entityPos, currentTime);

        // Check if AI has been running too long and should timeout
        if ((currentTime - lastStateChange) > AI_TIMEOUT)
        {
            DebugLogger.AIEvent("AI timeout reached - removing behavior",
                $"Has been running for {(currentTime - lastStateChange) / 1000f:F1} seconds",
                entity.EntityId.ToString());

            RemoveSelfFromEntity();
            return;
        }

        // Update AI logic based on current state
        switch (currentState)
        {
            case AIState.NavigatingToBase:
                HandleNavigatingToBase(entityPos, currentTime);
                break;

            case AIState.AttackingTarget:
                HandleAttackingTarget(entityPos, currentTime);
                break;

            case AIState.DestroyingDoor:
                HandleDestroyingDoor(entityPos, currentTime);
                break;
        }

        // Always check for targets while doing other activities
        CheckForTargets(entityPos, currentTime);

        // Execute movement based on current path
        ExecuteMovement(dt);
    }

    private void HandleNavigatingToBase(Vec3d entityPos, long currentTime)
    {
        // Check for doors/gates to prioritize FIRST - before anything else
        var nearbyDoor = FindNearbyDoor(entityPos);
        if (nearbyDoor != null)
        {
            DebugLogger.AIEvent("Door detected - switching to destruction mode",
                $"Door at {nearbyDoor.X:F1},{nearbyDoor.Y:F1},{nearbyDoor.Z:F1}",
                entity.EntityId.ToString());

            ChangeState(AIState.DestroyingDoor);
            return;
        }

        // Recalculate path periodically or if we don't have one
        if (currentPath.Count == 0 || (currentTime - lastPathCalculation) > PATH_RECALC_INTERVAL)
        {
            RecalculatePathToBase(entityPos);
            lastPathCalculation = currentTime;
        }

        // Check if we've reached the base area - but only give up if no doors found and very close
        var distanceToBase = entityPos.DistanceTo(baseCenter);
        if (distanceToBase < 2f) // Get very close before considering giving up
        {
            // Do one final check for doors before giving up
            var finalDoorCheck = FindNearbyDoor(entityPos);
            if (finalDoorCheck == null)
            {
                DebugLogger.AIEvent("Reached base center with no doors found - removing AI behavior",
                    $"Distance: {distanceToBase:F1}m, returning to default AI",
                    entity.EntityId.ToString());

                // Remove this AI behavior and let the entity return to default Vintage Story AI
                RemoveSelfFromEntity();
                return;
            }
            else
            {
                // Found a door even when very close to center, switch to attacking it
                DebugLogger.AIEvent("Found door at base center - switching to destruction",
                    $"Door at {finalDoorCheck.X:F1},{finalDoorCheck.Y:F1},{finalDoorCheck.Z:F1}",
                    entity.EntityId.ToString());
                ChangeState(AIState.DestroyingDoor);
                return;
            }
        }
    }

    private void HandleAttackingTarget(Vec3d entityPos, long currentTime)
    {
        if (currentTarget?.Alive != true || currentTarget.ServerPos == null)
        {
            DebugLogger.AIEvent("Target lost - returning to base navigation", "", entity.EntityId.ToString());
            ReturnToBaseNavigation();
            return;
        }

        var distanceToTarget = entityPos.DistanceTo(currentTarget.ServerPos.XYZ);

        // If target is too far, lose it and return to base navigation
        if (distanceToTarget > TARGET_DETECTION_RANGE)
        {
            if ((currentTime - lastTargetSeen) > TARGET_LOST_TIMEOUT)
            {
                DebugLogger.AIEvent("Target lost (timeout) - returning to base navigation",
                    $"Last seen {(currentTime - lastTargetSeen) / 1000f:F1}s ago",
                    entity.EntityId.ToString());

                ReturnToBaseNavigation();
                return;
            }
        }
        else
        {
            lastTargetSeen = currentTime;
        }

        // If close enough, attack
        if (distanceToTarget <= ATTACK_RANGE)
        {
            AttackTarget(currentTarget);
        }
        else
        {
            // Recalculate path to target periodically
            if (currentPath.Count == 0 || (currentTime - lastPathCalculation) > PATH_RECALC_INTERVAL / 2) // More frequent updates when chasing
            {
                RecalculatePathToTarget(entityPos, currentTarget);
                lastPathCalculation = currentTime;
            }
        }
    }

    private void HandleDestroyingDoor(Vec3d entityPos, long currentTime)
    {
        // Check if we've reached the base area after destroying doors
        var distanceToBase = entityPos.DistanceTo(baseCenter);
        if (distanceToBase < 5f) // Same increased radius
        {
            DebugLogger.AIEvent("Reached base area after door destruction - removing AI behavior",
                $"Distance: {distanceToBase:F1}m, returning to default AI",
                entity.EntityId.ToString());

            RemoveSelfFromEntity();
            return;
        }

        // Find the nearest door again (in case it moved or was destroyed)
        var nearbyDoor = FindNearbyDoor(entityPos);

        if (nearbyDoor == null)
        {
            DebugLogger.AIEvent("Door destroyed or not found", "Returning to base navigation", entity.EntityId.ToString());
            ReturnToBaseNavigation();
            return;
        }

        var distanceToDoor = entityPos.DistanceTo(nearbyDoor);

        if (distanceToDoor <= ATTACK_RANGE)
        {
            // Attack the door
            AttackDoor(nearbyDoor);
        }
        else
        {
            // Move towards door
            if (currentPath.Count == 0 || (currentTime - lastPathCalculation) > PATH_RECALC_INTERVAL / 2)
            {
                RecalculatePathToDoor(entityPos, nearbyDoor);
                lastPathCalculation = currentTime;
            }
        }
    }

    private void CheckForTargets(Vec3d entityPos, long currentTime)
    {
        // Don't interrupt door destruction unless target is very close
        if (currentState == AIState.DestroyingDoor)
        {
            var nearbyPlayer = targetDetection.FindNearestPlayer(entityPos, TARGET_DETECTION_RANGE / 2);
            if (nearbyPlayer != null)
            {
                var distance = entityPos.DistanceTo(nearbyPlayer.ServerPos.XYZ);
                if (distance <= ATTACK_RANGE * 1.5f) // Only switch if really close
                {
                    DebugLogger.AIEvent("Very close target detected during door destruction - switching to attack",
                        $"Player: {nearbyPlayer.GetName()}, Distance: {distance:F1}m",
                        entity.EntityId.ToString());

                    currentTarget = nearbyPlayer;
                    lastTargetSeen = currentTime;
                    ChangeState(AIState.AttackingTarget);
                }
            }
            return;
        }

        // Normal target detection
        var targetPlayer = targetDetection.FindNearestPlayer(entityPos, TARGET_DETECTION_RANGE);
        if (targetPlayer != null && currentTarget != targetPlayer)
        {
            DebugLogger.AIEvent("New target detected",
                $"Player: {targetPlayer.GetName()}, Distance: {entityPos.DistanceTo(targetPlayer.ServerPos.XYZ):F1}m",
                entity.EntityId.ToString());

            currentTarget = targetPlayer;
            lastTargetSeen = currentTime;
            ChangeState(AIState.AttackingTarget);
        }
    }

    private void CheckAndHandleStuckCondition(Vec3d entityPos, long currentTime)
    {
        var distanceMoved = entityPos.DistanceTo(lastPosition);

        if (distanceMoved > STUCK_MOVEMENT_THRESHOLD)
        {
            // Entity is moving, reset stuck detection
            lastPosition = entityPos.Clone();
            lastMovementTime = currentTime;
            isStuck = false;
        }
        else if ((currentTime - lastMovementTime) > STUCK_TIMEOUT)
        {
            // Entity is stuck
            if (!isStuck)
            {
                DebugLogger.AIEvent("Entity stuck detected",
                    $"No movement for {(currentTime - lastMovementTime) / 1000f:F1}s",
                    entity.EntityId.ToString());
                isStuck = true;
            }

            // Try to recover by moving to a random nearby position
            RecoverFromStuckPosition(entityPos);
            lastMovementTime = currentTime; // Reset timer
        }
    }

    private void RecoverFromStuckPosition(Vec3d entityPos)
    {
        // Clear current path
        currentPath.Clear();
        currentPathIndex = 0;

        // Generate random nearby position
        var random = sapi.World.Rand;
        var angle = random.NextDouble() * Math.PI * 2;
        var distance = 5 + random.NextDouble() * 10; // 5-15 blocks away

        var recoveryPos = new Vec3d(
            entityPos.X + Math.Cos(angle) * distance,
            entityPos.Y,
            entityPos.Z + Math.Sin(angle) * distance
        );

        DebugLogger.AIEvent("Attempting stuck recovery",
            $"Moving from {entityPos.X:F1},{entityPos.Y:F1},{entityPos.Z:F1} to {recoveryPos.X:F1},{recoveryPos.Y:F1},{recoveryPos.Z:F1}",
            entity.EntityId.ToString());

        // Try to path to recovery position
        var recoveryPath = pathfinder.FindPath(entityPos, recoveryPos);
        if (recoveryPath.Count > 0)
        {
            currentPath = recoveryPath;
            currentPathIndex = 0;
        }
        else
        {
            // If we can't path anywhere, try jumping
            TryJump();
        }

        lastPosition = entityPos.Clone();
        isStuck = false;
    }

    private void RecalculatePathToBase(Vec3d entityPos)
    {
        DebugLogger.AIEvent("Recalculating path to base",
            $"From {entityPos.X:F1},{entityPos.Y:F1},{entityPos.Z:F1} to {baseCenter.X:F1},{baseCenter.Y:F1},{baseCenter.Z:F1}",
            entity.EntityId.ToString());

        var path = pathfinder.FindPath(entityPos, baseCenter);
        if (path.Count > 0)
        {
            currentPath = path;
            currentPathIndex = 0;
            DebugLogger.AIEvent("Path to base calculated", $"Path length: {path.Count} nodes", entity.EntityId.ToString());

            // Show path visualization if enabled
            ShowPathVisualization(path, "ToBase");
        }
        else
        {
            DebugLogger.AIEvent("Failed to find path to base", "Trying direct movement", entity.EntityId.ToString());
            currentPath.Clear();
            ClearPathVisualization();
        }
    }

    private void RecalculatePathToTarget(Vec3d entityPos, Entity target)
    {
        if (target?.ServerPos == null) return;

        var targetPos = target.ServerPos.XYZ;
        var path = pathfinder.FindPath(entityPos, targetPos);

        if (path.Count > 0)
        {
            currentPath = path;
            currentPathIndex = 0;
            ShowPathVisualization(path, "ToTarget");
        }
        else
        {
            // If no path found, clear path and try direct movement
            currentPath.Clear();
            ClearPathVisualization();
        }
    }

    private void RecalculatePathToDoor(Vec3d entityPos, Vec3d doorPos)
    {
        var path = pathfinder.FindPath(entityPos, doorPos);

        if (path.Count > 0)
        {
            currentPath = path;
            currentPathIndex = 0;
            ShowPathVisualization(path, "ToDoor");
        }
        else
        {
            currentPath.Clear();
            ClearPathVisualization();
        }
    }

    private void ExecuteMovement(float dt)
    {
        if (entity?.ServerPos == null) return;

        var entityPos = entity.ServerPos.XYZ;
        Vec3d targetPosition;

        // Determine target position based on current state and path
        if (currentPath.Count > 0 && currentPathIndex < currentPath.Count)
        {
            targetPosition = currentPath[currentPathIndex];

            // Check if we've reached the current path node - reduced threshold for more precise navigation
            var distanceToNode = entityPos.DistanceTo(targetPosition);
            if (distanceToNode < 0.8f) // More precise node advancement
            {
                currentPathIndex++;
                if (currentPathIndex < currentPath.Count)
                {
                    targetPosition = currentPath[currentPathIndex];
                }
                else
                {
                    // Reached end of path
                    DebugLogger.AIEvent("Reached end of path", $"Path completed with {currentPath.Count} nodes", entity.EntityId.ToString());
                }
            }
        }
        else
        {
            // No path, move directly toward state-appropriate target
            targetPosition = currentState switch
            {
                AIState.AttackingTarget when currentTarget?.ServerPos != null => currentTarget.ServerPos.XYZ,
                AIState.DestroyingDoor => FindNearbyDoor(entityPos) ?? baseCenter,
                _ => baseCenter
            };
        }

        // Calculate direction and movement
        var direction = (targetPosition - entityPos).Normalize();
        var currentTime = sapi.World.ElapsedMilliseconds;

        // Check if we need to jump
        var heightDifference = targetPosition.Y - entityPos.Y;
        if (heightDifference > 0.5f && (currentTime - lastJumpTime) > JUMP_COOLDOWN)
        {
            TryJump();
        }

        // Apply movement using Vintage Story's physics system instead of direct motion manipulation
        ApplyMovementForces(direction, entityPos, targetPosition);
    }

    private void TryJump()
    {
        var currentTime = sapi.World.ElapsedMilliseconds;
        if ((currentTime - lastJumpTime) < JUMP_COOLDOWN) return;

        var entityPos = entity.ServerPos.XYZ;

        // Check what's in front of us to determine jump strength
        var forwardDirection = entity.ServerPos.GetViewVector().Normalize();
        var checkPos = entityPos + forwardDirection.ToVec3d() * 1.5;
        var blockPos = new BlockPos((int)checkPos.X, (int)(checkPos.Y + 1), (int)checkPos.Z);

        var heightDifference = 0f;
        if (sapi.World.BlockAccessor.IsValidPos(blockPos))
        {
            var blockAbove = sapi.World.BlockAccessor.GetBlock(blockPos);
            if (blockAbove.Id != 0) // There's a block
            {
                heightDifference = (float)(blockPos.Y - entityPos.Y);
            }
        }

        // Adjust jump force based on height needed - max 1 block high
        var jumpForce = 0.08f; // Reduced significantly to prevent 2-block jumps
        if (heightDifference > 0.8f)
        {
            jumpForce = 0.10f; // Slightly stronger for 1-block obstacles only
        }

        var motion = entity.ServerPos.Motion;
        motion.Y = jumpForce;
        entity.ServerPos.Motion = motion;

        lastJumpTime = currentTime;

        DebugLogger.AIEvent("Jump executed",
            $"Force: {jumpForce:F2}, Height difference: {heightDifference:F1}",
            entity.EntityId.ToString());
    }

    private Vec3d? FindNearbyDoor(Vec3d entityPos)
    {
        const int searchRadius = 12; // Increased radius to detect doors earlier
        Vec3d? nearestDoor = null;
        double nearestDistance = double.MaxValue;

        for (int x = -searchRadius; x <= searchRadius; x++)
        {
            for (int y = -3; y <= 3; y++) // Limited Y search
            {
                for (int z = -searchRadius; z <= searchRadius; z++)
                {
                    var checkPos = new BlockPos(
                        (int)entityPos.X + x,
                        (int)entityPos.Y + y,
                        (int)entityPos.Z + z
                    );

                    if (!sapi.World.BlockAccessor.IsValidPos(checkPos)) continue;

                    var block = sapi.World.BlockAccessor.GetBlock(checkPos);
                    var blockCode = block.Code?.ToString();

                    if (blockCode != null &&
                        (blockCode.Contains("door", StringComparison.OrdinalIgnoreCase) ||
                         blockCode.Contains("gate", StringComparison.OrdinalIgnoreCase)))
                    {
                        var doorPos = new Vec3d(checkPos.X + 0.5, checkPos.Y, checkPos.Z + 0.5);
                        var distance = entityPos.DistanceTo(doorPos);

                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestDoor = doorPos;
                        }
                    }
                }
            }
        }

        return nearestDoor;
    }

    private void AttackTarget(Entity target)
    {
        if (target is not EntityPlayer player) return;

        var damageSource = new DamageSource
        {
            Source = EnumDamageSource.Entity,
            SourceEntity = entity,
            Type = EnumDamageType.BluntAttack
        };

        DebugLogger.AIEvent("Attacking player",
            $"Target: {player.GetName()}",
            entity.EntityId.ToString());

        if (player.Player != null)
        {
            player.ReceiveDamage(damageSource, 2f); // 2 damage per attack
        }
    }

    private void AttackDoor(Vec3d doorPos)
    {
        var blockPos = new BlockPos((int)doorPos.X, (int)doorPos.Y, (int)doorPos.Z);
        var block = sapi.World.BlockAccessor.GetBlock(blockPos);

        if (block.Id == 0)
        {
            // Door already destroyed, unregister from manager and return to base navigation
            doorHealthManager?.UnregisterAttacker(doorPos, entity.EntityId);
            ReturnToBaseNavigation();
            return;
        }

        // Try to register as an attacker (limit 3 per door)
        if (doorHealthManager != null && !doorHealthManager.TryRegisterAttacker(doorPos, entity.EntityId))
        {
            // Cannot attack this door (max attackers reached), return to base navigation
            DebugLogger.AIEvent("Cannot attack door - max attackers reached",
                $"Door at {doorPos.X:F1},{doorPos.Y:F1},{doorPos.Z:F1}",
                entity.EntityId.ToString());
            ReturnToBaseNavigation();
            return;
        }

        // Trigger attack animation
        TriggerAttackAnimation();

        // Attack the door with health system
        bool doorDestroyed = doorHealthManager?.AttackDoor(doorPos, entity.EntityId, 50f) ?? false;

        if (doorDestroyed)
        {
            // Door was destroyed, remove the block
            sapi.World.BlockAccessor.SetBlock(0, blockPos);

            DebugLogger.AIEvent("Door destroyed by health system",
                $"Block: {block.Code} at {doorPos.X:F1},{doorPos.Y:F1},{doorPos.Z:F1}",
                entity.EntityId.ToString());

            // Return to base navigation after destroying door
            ReturnToBaseNavigation();
        }
        else
        {
            DebugLogger.AIEvent("Attacking door with health system",
                $"Block: {block.Code} at {doorPos.X:F1},{doorPos.Y:F1},{doorPos.Z:F1}",
                entity.EntityId.ToString());
        }
    }

    /// <summary>
    /// Trigger attack animation for the entity
    /// </summary>
    private void TriggerAttackAnimation()
    {
        try
        {
            // Try to trigger attack animation if the entity supports it
            if (entity is EntityAgent entityAgent)
            {
                // Look for common attack animations in drifters
                var animationManager = entityAgent.AnimManager;
                if (animationManager != null)
                {
                    // Try common attack animation names
                    var attackAnimations = new[] { "attack", "meleeattack", "strike", "hit" };

                    foreach (var animName in attackAnimations)
                    {
                        if (animationManager.GetAnimationState(animName) != null)
                        {
                            animationManager.StartAnimation(animName);
                            DebugLogger.AIEvent("Attack animation triggered",
                                $"Animation: {animName}",
                                entity.EntityId.ToString());
                            return;
                        }
                    }

                    // If no specific attack animation found, log available animations for debugging
                    DebugLogger.AIEvent("No attack animation found",
                        "Entity may not support attack animations",
                        entity.EntityId.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"Failed to trigger attack animation for entity {entity.EntityId}", ex);
        }
    }

    private void ChangeState(AIState newState)
    {
        if (currentState == newState) return;

        var oldState = currentState;
        currentState = newState;
        lastStateChange = sapi.World.ElapsedMilliseconds;

        // Clear path when changing states to force recalculation
        currentPath.Clear();
        currentPathIndex = 0;

        DebugLogger.AIEvent("State changed",
            $"From {oldState} to {newState}",
            entity.EntityId.ToString());
    }

    private void ReturnToBaseNavigation()
    {
        currentTarget = null;
        ChangeState(AIState.NavigatingToBase);
    }

    /// <summary>
    /// Apply movement forces using Vintage Story's physics system
    /// </summary>
    private void ApplyMovementForces(Vec3d direction, Vec3d entityPos, Vec3d targetPosition)
    {
        if (entity?.ServerPos == null) return;

        // Check if entity has controlled physics behavior
        // Note: Many entities may not have this behavior, so fallback to direct motion is normal
        if (!entity.HasBehavior("controlledphysics"))
        {
            // Use improved direct motion that simulates physics behavior
            ApplyDirectMotion(direction);
            return;
        }

        // Calculate control forces instead of direct motion
        var moveSpeed = 0.05f; // Control force strength - higher values = more responsive movement
        var controlForce = direction.Normalize() * moveSpeed;

        // Apply the control force through the physics system
        // This works with terrain collision, drag, and other physics modules
        entity.ServerPos.Motion.Add(controlForce.X * 0.3f, 0, controlForce.Z * 0.3f);

        // Make entity face the movement direction
        if (direction.X != 0 || direction.Z != 0)
        {
            var yaw = (float)Math.Atan2(direction.X, direction.Z);
            entity.ServerPos.Yaw = yaw;
            entity.Pos.Yaw = yaw;
        }

        // Check if we're making progress or getting stuck
        CheckMovementProgress(entityPos);
    }

    /// <summary>
    /// Fallback direct motion for entities without controlled physics
    /// </summary>
    private void ApplyDirectMotion(Vec3d direction)
    {
        var moveSpeed = 0.02f;
        var motion = entity.ServerPos.Motion;

        // Apply drag-like behavior to make movement more natural
        var dragFactor = 0.8f; // Reduce existing motion for more responsive control
        motion.X *= dragFactor;
        motion.Z *= dragFactor;

        // Add new movement forces
        motion.X += direction.X * moveSpeed;
        motion.Z += direction.Z * moveSpeed;

        // Cap maximum horizontal speed to prevent runaway velocity
        var maxSpeed = 0.1f;
        var horizontalSpeed = Math.Sqrt(motion.X * motion.X + motion.Z * motion.Z);
        if (horizontalSpeed > maxSpeed)
        {
            var scale = maxSpeed / horizontalSpeed;
            motion.X *= scale;
            motion.Z *= scale;
        }

        entity.ServerPos.Motion = motion;

        // Make entity face the movement direction
        if (direction.X != 0 || direction.Z != 0)
        {
            var yaw = (float)Math.Atan2(direction.X, direction.Z);
            entity.ServerPos.Yaw = yaw;
            entity.Pos.Yaw = yaw;
        }
    }

    /// <summary>
    /// Check if entity is making movement progress or is stuck
    /// </summary>
    private void CheckMovementProgress(Vec3d entityPos)
    {
        var currentTime = sapi.World.ElapsedMilliseconds;
        var distanceFromLastPos = entityPos.DistanceTo(lastPosition);

        if (distanceFromLastPos < 0.1f) // Not moving much
        {
            if (currentTime - lastMovementTime > STUCK_TIMEOUT)
            {
                if (!isStuck)
                {
                    isStuck = true;
                    DebugLogger.AIEvent("Entity appears stuck",
                        $"No movement for {STUCK_TIMEOUT / 1000f}s, forcing path recalculation",
                        entity.EntityId.ToString());

                    // Force immediate path recalculation
                    lastPathCalculation = 0;
                    currentPath.Clear();
                }
            }
        }
        else
        {
            // Entity is moving, reset stuck detection
            lastMovementTime = currentTime;
            lastPosition = entityPos;
            isStuck = false;
        }
    }

    /// <summary>
    /// Remove this AI behavior from the entity and let it return to default AI
    /// </summary>
    private void RemoveSelfFromEntity()
    {
        try
        {
            // Unregister from any door attacks if currently attacking doors
            if (currentState == AIState.DestroyingDoor && doorHealthManager != null)
            {
                var nearbyDoor = FindNearbyDoor(entity.ServerPos.XYZ);
                if (nearbyDoor != null)
                {
                    doorHealthManager.UnregisterAttacker(nearbyDoor, entity.EntityId);
                    DebugLogger.AIEvent("Unregistered from door attack during cleanup",
                        $"Door at {nearbyDoor.X:F1},{nearbyDoor.Y:F1},{nearbyDoor.Z:F1}",
                        entity.EntityId.ToString());
                }
            }

            // Clear any custom attributes we set
            entity.WatchedAttributes.RemoveAttribute("baseTargetX");
            entity.WatchedAttributes.RemoveAttribute("baseTargetY");
            entity.WatchedAttributes.RemoveAttribute("baseTargetZ");
            entity.WatchedAttributes.RemoveAttribute("baseType");
            entity.WatchedAttributes.RemoveAttribute("targetX");
            entity.WatchedAttributes.RemoveAttribute("targetY");
            entity.WatchedAttributes.RemoveAttribute("targetZ");
            entity.WatchedAttributes.RemoveAttribute("spawningSystem");
            entity.WatchedAttributes.RemoveAttribute("doorHitCount");

            // Stop any current movement
            entity.ServerPos.Motion.Set(0, entity.ServerPos.Motion.Y, 0);

            // Remove this behavior from the entity
            entity.RemoveBehavior(this);

            DebugLogger.AIEvent("AI behavior removed successfully",
                "Entity returned to default Vintage Story AI",
                entity.EntityId.ToString());
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"Failed to remove AI behavior from entity {entity.EntityId}", ex);
        }
    }

    /// <summary>
    /// Show path visualization if debug mode is enabled
    /// </summary>
    private void ShowPathVisualization(List<Vec3d> path, string pathType)
    {
        if (debugVisualization?.IsPathVisualizationEnabled == true)
        {
            debugVisualization.ShowEntityPath(entity.EntityId.ToString(), path);
        }
    }

    /// <summary>
    /// Clear path visualization for this entity
    /// </summary>
    private void ClearPathVisualization()
    {
        debugVisualization?.ClearEntityPathParticles(entity.EntityId.ToString());
    }

    /// <summary>
    /// Initialize debug visualization (static, shared across entities)
    /// </summary>
    public static void SetDebugVisualization(DebugVisualization? visualization)
    {
        debugVisualization = visualization;
    }

    public override string PropertyName() => "ai";
}

/// <summary>
/// AI States for the behavior
/// </summary>
public enum AIState
{
    NavigatingToBase,
    AttackingTarget,
    DestroyingDoor
}
