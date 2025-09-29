using System;
using System.Linq;
using HueHordes.Debug;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Handles target detection and validation for AI entities
/// Focuses on finding players while ignoring creative/spectator mode players
/// </summary>
public class TargetDetection
{
    private readonly ICoreServerAPI sapi;

    public TargetDetection(ICoreServerAPI serverApi)
    {
        sapi = serverApi ?? throw new ArgumentNullException(nameof(serverApi));
    }

    /// <summary>
    /// Find the nearest valid player target within the specified range
    /// </summary>
    public Entity? FindNearestPlayer(Vec3d position, float maxRange)
    {
        Entity? nearestPlayer = null;
        double nearestDistance = double.MaxValue;

        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            if (player?.Entity?.ServerPos == null || !player.Entity.Alive)
                continue;

            // Skip creative/spectator mode players
            if (IsPlayerInCreativeOrSpectator(player))
                continue;

            var distance = position.DistanceTo(player.Entity.ServerPos.XYZ);

            if (distance <= maxRange && distance < nearestDistance)
            {
                // Additional line of sight check could be added here
                if (HasLineOfSight(position, player.Entity.ServerPos.XYZ))
                {
                    nearestDistance = distance;
                    nearestPlayer = player.Entity;
                }
            }
        }

        if (nearestPlayer != null)
        {
            DebugLogger.AITarget("TargetDetection", "Player",
                nearestPlayer.GetName(),
                $"Found at distance {nearestDistance:F1}m");
        }

        return nearestPlayer;
    }

    /// <summary>
    /// Check if a player is in creative or spectator mode
    /// </summary>
    private bool IsPlayerInCreativeOrSpectator(IPlayer player)
    {
        if (player is not IServerPlayer serverPlayer)
            return false;

        // Check game mode
        var gameMode = serverPlayer.WorldData?.CurrentGameMode;

        return gameMode == EnumGameMode.Creative || gameMode == EnumGameMode.Spectator;
    }

    /// <summary>
    /// Basic line of sight check to see if target is visible
    /// </summary>
    private bool HasLineOfSight(Vec3d from, Vec3d to)
    {
        // Simple line of sight - check if there are solid blocks in the way
        var direction = (to - from).Normalize();
        var distance = from.DistanceTo(to);
        var steps = (int)(distance / 0.5f); // Check every 0.5 blocks

        // Adjust start position to be at eye level
        var startPos = from.AddCopy(0, 1.5, 0);
        var endPos = to.AddCopy(0, 1.5, 0);

        for (int i = 1; i <= steps; i++)
        {
            var checkPos = startPos + direction * (distance * i / steps);
            var blockPos = new BlockPos((int)checkPos.X, (int)checkPos.Y, (int)checkPos.Z);

            if (!sapi.World.BlockAccessor.IsValidPos(blockPos))
                continue;

            var block = sapi.World.BlockAccessor.GetBlock(blockPos);

            // If there's a solid block blocking the view
            if (block.Id != 0 && block.CollisionBoxes?.Length > 0)
            {
                // Allow seeing through glass, fences, etc.
                var blockCode = block.Code?.ToString();
                if (blockCode != null && !IsTransparentBlock(blockCode))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Check if a block type should be considered transparent for line of sight
    /// </summary>
    private bool IsTransparentBlock(string blockCode)
    {
        return blockCode.Contains("glass", StringComparison.OrdinalIgnoreCase) ||
               blockCode.Contains("fence", StringComparison.OrdinalIgnoreCase) ||
               blockCode.Contains("bars", StringComparison.OrdinalIgnoreCase) ||
               blockCode.Contains("grate", StringComparison.OrdinalIgnoreCase) ||
               blockCode.Contains("lattice", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Find all valid player targets within range
    /// </summary>
    public Entity[] FindAllPlayersInRange(Vec3d position, float maxRange)
    {
        var validTargets = sapi.World.AllOnlinePlayers
            .Where(player => player?.Entity?.ServerPos != null && player.Entity.Alive)
            .Where(player => !IsPlayerInCreativeOrSpectator(player))
            .Where(player => position.DistanceTo(player.Entity.ServerPos.XYZ) <= maxRange)
            .Where(player => HasLineOfSight(position, player.Entity.ServerPos.XYZ))
            .Select(player => player.Entity)
            .ToArray();

        return validTargets;
    }

    /// <summary>
    /// Check if a specific entity is a valid target
    /// </summary>
    public bool IsValidTarget(Entity entity)
    {
        if (entity?.Alive != true)
            return false;

        // Check if it's a player
        if (entity is EntityPlayer entityPlayer)
        {
            var player = sapi.World.PlayerByUid(entityPlayer.PlayerUID);
            return player != null && !IsPlayerInCreativeOrSpectator(player);
        }

        // Could extend this to include other entity types (NPCs, animals, etc.)
        return false;
    }

    /// <summary>
    /// Get the closest point on the target that we should aim for
    /// </summary>
    public Vec3d GetTargetAimPoint(Entity target)
    {
        if (target?.ServerPos == null)
            return Vec3d.Zero;

        // Aim for center mass (slightly above ground level)
        return target.ServerPos.XYZ.AddCopy(0, 0.8, 0);
    }

    /// <summary>
    /// Predict where a moving target will be
    /// </summary>
    public Vec3d PredictTargetPosition(Entity target, float predictionTime)
    {
        if (target?.ServerPos == null)
            return Vec3d.Zero;

        var currentPos = target.ServerPos.XYZ;
        var velocity = target.ServerPos.Motion;

        // Simple linear prediction
        var predictedPos = currentPos + velocity * predictionTime;

        return predictedPos.AddCopy(0, 0.8, 0); // Add height for aiming
    }
}
