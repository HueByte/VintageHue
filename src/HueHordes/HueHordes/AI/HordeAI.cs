using System;
using System.Collections.Generic;
using HueHordes.Behaviors;
using HueHordes.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Main coordinator for the advanced horde AI system
/// </summary>
public class HordeAI
{
    private readonly ICoreServerAPI sapi;
    private readonly BaseDetection baseDetection;
    private readonly SmartTargeting smartTargeting;

    private static HordeAI? instance;

    public static HordeAI? Instance => instance;

    public BaseDetection BaseDetection => baseDetection;
    public SmartTargeting SmartTargeting => smartTargeting;

    public HordeAI(ICoreServerAPI serverApi)
    {
        sapi = serverApi;
        baseDetection = new BaseDetection(serverApi);
        smartTargeting = new SmartTargeting(serverApi, baseDetection);

        instance = this; // Set singleton instance
    }

    /// <summary>
    /// Calculate smart spawn positions outside player base
    /// </summary>
    public List<Vec3d> CalculateSpawnPositions(IServerPlayer player, int count)
    {
        // First, detect or get the player's base
        var playerBase = baseDetection.DetectPlayerBase(player);

        if (playerBase != null)
        {
            // Get spawn positions outside the base
            var spawnPositions = playerBase.GetSafeSpawnPositions(count);

            // Adjust for terrain height
            var adjustedPositions = new List<Vec3d>();
            foreach (var pos in spawnPositions)
            {
                var terrainHeight = GetTerrainHeight(pos);
                adjustedPositions.Add(new Vec3d(pos.X, terrainHeight + 1, pos.Z));
            }

            return adjustedPositions;
        }
        else
        {
            // Fallback to traditional ring spawning around player
            return CalculateRingSpawnPositions(player.Entity.ServerPos.XYZ, count, 15f, 30f);
        }
    }

    /// <summary>
    /// Spawn a horde with advanced AI for a specific player
    /// </summary>
    public void SpawnSmartHorde(IServerPlayer player, ServerConfig config, double currentDay)
    {
        var spawnPositions = CalculateSpawnPositions(player, config.Count);
        var initialPlayerPos = player.Entity.ServerPos.XYZ.Clone();

        sapi.Logger.Debug($"[HordeAI] Spawning smart horde for {player.PlayerName} with {spawnPositions.Count} entities");

        for (int i = 0; i < config.Count && i < spawnPositions.Count; i++)
        {
            var entityCode = config.EntityCodes[i % config.EntityCodes.Length];
            var spawnPos = spawnPositions[i];

            TrySpawnSmartEntity(entityCode, spawnPos, player, config, currentDay);
        }
    }

    /// <summary>
    /// Spawn a single entity with smart AI behavior
    /// </summary>
    private void TrySpawnSmartEntity(string entityCode, Vec3d spawnAt, IServerPlayer targetPlayer, ServerConfig config, double currentDay)
    {
        try
        {
            var loc = new AssetLocation(entityCode);
            if (string.IsNullOrEmpty(loc.Domain))
                loc = new AssetLocation("game", loc.Path);

            var etype = sapi.World.GetEntityType(loc);
            if (etype == null)
            {
                sapi.Logger.Warning($"[HordeAI] Unknown entity '{entityCode}'");
                return;
            }

            var entity = sapi.World.ClassRegistry.CreateEntity(etype);
            entity.ServerPos.SetPos(spawnAt);
            entity.Pos.SetFrom(entity.ServerPos);

            // Spawn the entity first
            sapi.World.SpawnEntity(entity);

            // Set up smart AI attributes
            entity.WatchedAttributes.SetString("hordeTargetPlayerUID", targetPlayer.PlayerUID);
            entity.WatchedAttributes.SetDouble("hordeOriginalTargetX", targetPlayer.Entity.ServerPos.X);
            entity.WatchedAttributes.SetDouble("hordeOriginalTargetY", targetPlayer.Entity.ServerPos.Y);
            entity.WatchedAttributes.SetDouble("hordeOriginalTargetZ", targetPlayer.Entity.ServerPos.Z);
            entity.WatchedAttributes.SetFloat("hordeBehaviorDuration", config.NudgeSeconds * 3); // Longer duration for smart AI
            entity.WatchedAttributes.SetFloat("hordeSpeed", config.NudgeSpeed * 1.5f); // Slightly faster

            // Add smart behavior instead of simple nudge
            if (config.NudgeTowardInitialPos && entity is EntityAgent)
            {
                try
                {
                    var smartBehavior = new SmartHordeBehavior(entity);
                    entity.AddBehavior(smartBehavior);

                    sapi.Logger.Debug($"[HordeAI] Added smart behavior to {entityCode} at {spawnAt}");
                }
                catch (Exception ex)
                {
                    sapi.Logger.Warning($"[HordeAI] Failed to add smart behavior to entity {entityCode}: {ex.Message}");

                    // Fallback to simple behavior if smart AI fails
                    try
                    {
                        var fallbackBehavior = new HordeNudgeBehavior(entity);
                        entity.AddBehavior(fallbackBehavior);
                    }
                    catch (Exception fallbackEx)
                    {
                        sapi.Logger.Error($"[HordeAI] Fallback behavior also failed: {fallbackEx.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[HordeAI] Failed to spawn smart entity {entityCode}: {ex.Message}");
        }
    }

    /// <summary>
    /// Get terrain height at a position
    /// </summary>
    private int GetTerrainHeight(Vec3d position)
    {
        var blockPos = new BlockPos((int)position.X, 0, (int)position.Z);
        return sapi.World.BlockAccessor.GetTerrainMapheightAt(blockPos);
    }

    /// <summary>
    /// Fallback ring spawn calculation for when base detection fails
    /// </summary>
    private List<Vec3d> CalculateRingSpawnPositions(Vec3d center, int count, float minRadius, float maxRadius)
    {
        var positions = new List<Vec3d>();
        var rand = new Random();

        for (int i = 0; i < count; i++)
        {
            var angle = (2 * Math.PI * i) / count + (rand.NextDouble() - 0.5) * 0.5; // Add some randomness
            var radius = minRadius + (float)(rand.NextDouble() * (maxRadius - minRadius));

            var x = center.X + Math.Cos(angle) * radius;
            var z = center.Z + Math.Sin(angle) * radius;
            var y = GetTerrainHeight(new Vec3d(x, 0, z)) + 1;

            positions.Add(new Vec3d(x, y, z));
        }

        return positions;
    }

    /// <summary>
    /// Get base information for a player (for external queries)
    /// </summary>
    public PlayerBase? GetPlayerBaseInfo(string playerUID)
    {
        return baseDetection.GetPlayerBase(playerUID);
    }

    /// <summary>
    /// Force refresh base detection for a player
    /// </summary>
    public PlayerBase? RefreshPlayerBase(IServerPlayer player)
    {
        return baseDetection.DetectPlayerBase(player, 60); // Larger scan radius
    }

    /// <summary>
    /// Get statistics about the AI system
    /// </summary>
    public string GetAIStats()
    {
        var stats = new System.Text.StringBuilder();
        stats.AppendLine("=== Horde AI System Stats ===");
        stats.AppendLine($"Base Detection: Active");
        stats.AppendLine($"Smart Targeting: Active");

        // Could add more detailed stats here
        return stats.ToString();
    }
}