using System;
using System.Collections.Generic;
using System.Linq;
using HueHordes.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Advanced targeting system for horde AI that prioritizes targets dynamically
/// </summary>
public class SmartTargeting
{
    private readonly ICoreServerAPI sapi;
    private readonly BaseDetection baseDetection;

    public SmartTargeting(ICoreServerAPI serverApi, BaseDetection baseDetectionSystem)
    {
        sapi = serverApi;
        baseDetection = baseDetectionSystem;
    }

    /// <summary>
    /// Get the best target for an entity from the given position
    /// </summary>
    public HordeTarget? GetBestTarget(Vec3d fromPosition, string originalPlayerUID, double maxRange = 100.0)
    {
        var candidates = new List<HordeTarget>();
        var currentTime = sapi.World.Calendar.TotalDays;

        // 1. Look for player targets (highest priority)
        AddPlayerTargets(candidates, fromPosition, originalPlayerUID, maxRange, currentTime);

        // 2. Look for base-related targets
        AddBaseTargets(candidates, fromPosition, originalPlayerUID, currentTime);

        // 3. Look for other entity targets
        AddEntityTargets(candidates, fromPosition, maxRange, currentTime);

        // 4. Add patrol targets if no active targets found
        if (candidates.Count == 0 || candidates.All(t => t.Priority < 50))
        {
            AddPatrolTargets(candidates, fromPosition, originalPlayerUID, currentTime);
        }

        // Select best target based on priority and distance
        return SelectBestTarget(candidates, fromPosition);
    }

    /// <summary>
    /// Add player targets to candidate list
    /// </summary>
    private void AddPlayerTargets(List<HordeTarget> candidates, Vec3d fromPosition, string originalPlayerUID, double maxRange, double currentTime)
    {
        var maxRangeSquared = maxRange * maxRange;

        foreach (var player in sapi.World.AllOnlinePlayers.OfType<IServerPlayer>())
        {
            if (player.Entity?.Alive != true) continue;

            var playerPos = player.Entity.ServerPos.XYZ;
            var distanceSquared = fromPosition.SquareDistanceTo(playerPos);

            if (distanceSquared <= maxRangeSquared)
            {
                var target = new HordeTarget
                {
                    Position = playerPos.Clone(),
                    Type = TargetType.Player,
                    Priority = player.PlayerUID == originalPlayerUID ? 120 : 100, // Prefer original target
                    TargetPlayer = player,
                    LastSeenTime = currentTime,
                    Distance = Math.Sqrt(distanceSquared),
                    IsVisible = true,
                    ValidityDuration = 2000f // 2 seconds
                };

                candidates.Add(target);
            }
        }
    }

    /// <summary>
    /// Add base-related targets (entrances, center)
    /// </summary>
    private void AddBaseTargets(List<HordeTarget> candidates, Vec3d fromPosition, string originalPlayerUID, double currentTime)
    {
        var playerBase = baseDetection.GetPlayerBase(originalPlayerUID);
        if (playerBase == null) return;

        // Add entrance targets
        foreach (var entrance in playerBase.Entrances)
        {
            var distance = Math.Sqrt(fromPosition.SquareDistanceTo(entrance));

            var target = new HordeTarget
            {
                Position = entrance.Clone(),
                Type = TargetType.BaseEntrance,
                Priority = 60,
                RelatedBase = playerBase,
                LastSeenTime = currentTime,
                Distance = distance,
                IsVisible = true,
                ValidityDuration = 5000f // 5 seconds
            };

            candidates.Add(target);
        }

        // Add base center target
        var centerDistance = Math.Sqrt(fromPosition.SquareDistanceTo(playerBase.Center));
        var centerTarget = new HordeTarget
        {
            Position = playerBase.Center.Clone(),
            Type = TargetType.BaseCenter,
            Priority = 40,
            RelatedBase = playerBase,
            LastSeenTime = currentTime,
            Distance = centerDistance,
            IsVisible = true,
            ValidityDuration = 10000f // 10 seconds
        };

        candidates.Add(centerTarget);
    }

    /// <summary>
    /// Add other entity targets
    /// </summary>
    private void AddEntityTargets(List<HordeTarget> candidates, Vec3d fromPosition, double maxRange, double currentTime)
    {
        var maxRangeSquared = maxRange * maxRange;

        // Look for other entities (animals, NPCs) within range
        var nearbyEntities = sapi.World.GetEntitiesAround(fromPosition, (float)maxRange, (float)maxRange);

        foreach (var entity in nearbyEntities.OfType<EntityAgent>())
        {
            if (!entity.Alive || entity.Code.Path.Contains("drifter")) continue; // Skip other horde entities

            var entityPos = entity.ServerPos.XYZ;
            var distanceSquared = fromPosition.SquareDistanceTo(entityPos);

            if (distanceSquared <= maxRangeSquared)
            {
                var target = new HordeTarget
                {
                    Position = entityPos.Clone(),
                    Type = TargetType.Entity,
                    Priority = 80,
                    TargetEntity = entity,
                    LastSeenTime = currentTime,
                    Distance = Math.Sqrt(distanceSquared),
                    IsVisible = true,
                    ValidityDuration = 1500f // 1.5 seconds
                };

                candidates.Add(target);
            }
        }
    }

    /// <summary>
    /// Add patrol targets when no active targets are available
    /// </summary>
    private void AddPatrolTargets(List<HordeTarget> candidates, Vec3d fromPosition, string originalPlayerUID, double currentTime)
    {
        var playerBase = baseDetection.GetPlayerBase(originalPlayerUID);
        if (playerBase == null) return;

        // Add a random patrol point
        var rand = new Random();
        var patrolPoint = playerBase.GetRandomPatrolPoint(rand);
        var distance = Math.Sqrt(fromPosition.SquareDistanceTo(patrolPoint));

        var target = new HordeTarget
        {
            Position = patrolPoint,
            Type = TargetType.PatrolPoint,
            Priority = 20,
            RelatedBase = playerBase,
            LastSeenTime = currentTime,
            Distance = distance,
            IsVisible = true,
            ValidityDuration = 15000f, // 15 seconds
            CustomData = "patrol"
        };

        candidates.Add(target);
    }

    /// <summary>
    /// Select the best target from candidates
    /// </summary>
    private HordeTarget? SelectBestTarget(List<HordeTarget> candidates, Vec3d fromPosition)
    {
        if (candidates.Count == 0) return null;

        // Calculate dynamic priorities based on distance and other factors
        foreach (var candidate in candidates)
        {
            candidate.Priority = candidate.CalculateDynamicPriority(fromPosition);
        }

        // Sort by priority (descending), then by distance (ascending)
        var sortedCandidates = candidates
            .Where(c => c.IsValid(sapi.World.Calendar.TotalDays))
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Distance)
            .ToList();

        return sortedCandidates.FirstOrDefault();
    }

    /// <summary>
    /// Check if target is visible from current position (simplified line-of-sight)
    /// </summary>
    public bool HasLineOfSight(Vec3d fromPosition, Vec3d toPosition, float maxDistance = 50f)
    {
        var distance = fromPosition.DistanceTo(toPosition);
        if (distance > maxDistance) return false;

        var blockAccessor = sapi.World.BlockAccessor;
        var direction = (toPosition - fromPosition).Normalize();
        var stepSize = 1.0;
        var steps = (int)(distance / stepSize);

        // Check for blocking terrain between positions
        for (int i = 1; i < steps; i++)
        {
            var checkPos = fromPosition + direction * (i * stepSize);
            var blockPos = new BlockPos((int)checkPos.X, (int)checkPos.Y + 1, (int)checkPos.Z); // Check at eye level

            var block = blockAccessor.GetBlock(blockPos);
            if (block.Code.Path != "air" && block.CollisionBoxes?.Length > 0)
            {
                return false; // Line of sight blocked
            }
        }

        return true;
    }

    /// <summary>
    /// Update target information for dynamic tracking
    /// </summary>
    public void UpdateTarget(HordeTarget target)
    {
        if (target == null) return;

        var currentTime = sapi.World.Calendar.TotalDays;
        target.UpdatePosition(currentTime);

        // Update visibility if it's a player/entity target
        if (target.Type == TargetType.Player || target.Type == TargetType.Entity)
        {
            // Could add more sophisticated visibility checking here
            target.IsVisible = target.IsValid(currentTime);
        }
    }

    /// <summary>
    /// Get fallback target when current target is lost
    /// </summary>
    public HordeTarget? GetFallbackTarget(Vec3d lastKnownPosition, string originalPlayerUID, HordeTarget? lostTarget)
    {
        // First, try to get any nearby player
        var nearbyTarget = GetBestTarget(lastKnownPosition, originalPlayerUID, 30.0);
        if (nearbyTarget?.Type == TargetType.Player)
        {
            return nearbyTarget;
        }

        // If no players nearby, return to base center or entrance
        var playerBase = baseDetection.GetPlayerBase(originalPlayerUID);
        if (playerBase != null)
        {
            var currentTime = sapi.World.Calendar.TotalDays;

            // Prefer entrance if we were targeting a player
            if (lostTarget?.Type == TargetType.Player && playerBase.Entrances.Count > 0)
            {
                var entrance = playerBase.GetNearestEntrance(lastKnownPosition);
                return new HordeTarget
                {
                    Position = entrance,
                    Type = TargetType.BaseEntrance,
                    Priority = 50,
                    RelatedBase = playerBase,
                    LastSeenTime = currentTime,
                    Distance = Math.Sqrt(lastKnownPosition.SquareDistanceTo(entrance)),
                    IsVisible = true,
                    ValidityDuration = 10000f
                };
            }

            // Otherwise, go to base center for patrol
            return new HordeTarget
            {
                Position = playerBase.Center,
                Type = TargetType.BaseCenter,
                Priority = 30,
                RelatedBase = playerBase,
                LastSeenTime = currentTime,
                Distance = Math.Sqrt(lastKnownPosition.SquareDistanceTo(playerBase.Center)),
                IsVisible = true,
                ValidityDuration = 15000f,
                CustomData = "fallback"
            };
        }

        return null;
    }
}