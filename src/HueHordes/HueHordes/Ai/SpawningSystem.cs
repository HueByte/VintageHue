using System;
using System.Collections.Generic;
using HueHordes.Debug;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Clean spawning system that handles radius-based spawning around players
/// Step 1: Basic spawning functionality
/// </summary>
public class SpawningSystem
{
    private readonly ICoreServerAPI sapi;
    private readonly DoorHealthManager doorHealthManager;

    public SpawningSystem(ICoreServerAPI serverApi, DoorHealthManager doorHealthManager)
    {
        sapi = serverApi ?? throw new ArgumentNullException(nameof(serverApi));
        this.doorHealthManager = doorHealthManager ?? throw new ArgumentNullException(nameof(doorHealthManager));
    }

    /// <summary>
    /// Spawn entities in a radius around a player position
    /// </summary>
    public List<Entity> SpawnAroundPlayer(IServerPlayer player, SpawnConfig config)
    {
        if (player?.Entity?.ServerPos == null)
        {
            DebugLogger.AIEvent("Spawn failed", "Player or position is null", "SpawningSystem");
            return new List<Entity>();
        }

        var playerPos = player.Entity.ServerPos.XYZ;
        var spawnedEntities = new List<Entity>();

        DebugLogger.AIEvent("Starting spawn",
            $"Player: {player.PlayerName}, Count: {config.EntityCount}, Radius: {config.MinRadius}-{config.MaxRadius}",
            "SpawningSystem");

        for (int i = 0; i < config.EntityCount; i++)
        {
            var spawnPos = FindValidSpawnPosition(playerPos, config);
            if (spawnPos == null)
            {
                DebugLogger.AIEvent("Spawn position failed", $"Attempt {i + 1}", "SpawningSystem");
                continue;
            }

            var entity = CreateEntity(config.EntityCode, spawnPos, playerPos);
            if (entity != null)
            {
                spawnedEntities.Add(entity);

                DebugLogger.AISpawn(
                    config.EntityCode,
                    $"{spawnPos.X:F1},{spawnPos.Y:F1},{spawnPos.Z:F1}",
                    $"Player@{playerPos.X:F1},{playerPos.Y:F1},{playerPos.Z:F1}"
                );
            }
        }

        DebugLogger.AIEvent("Spawn completed",
            $"Spawned {spawnedEntities.Count}/{config.EntityCount} entities",
            "SpawningSystem");

        return spawnedEntities;
    }

    /// <summary>
    /// Find a valid spawn position within the specified radius
    /// </summary>
    private Vec3d? FindValidSpawnPosition(Vec3d centerPos, SpawnConfig config)
    {
        const int maxAttempts = 10;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Generate random position in radius
            var angle = sapi.World.Rand.NextDouble() * Math.PI * 2;
            var radius = config.MinRadius + sapi.World.Rand.NextDouble() * (config.MaxRadius - config.MinRadius);

            var x = centerPos.X + Math.Cos(angle) * radius;
            var z = centerPos.Z + Math.Sin(angle) * radius;

            // Find ground level
            var groundY = FindGroundLevel(x, z);
            if (groundY == null) continue;

            var spawnPos = new Vec3d(x, groundY.Value + 1, z);

            // Check if position is valid
            if (IsValidSpawnPosition(spawnPos))
            {
                return spawnPos;
            }
        }

        return null;
    }

    /// <summary>
    /// Find the ground level at given X, Z coordinates
    /// </summary>
    private int? FindGroundLevel(double x, double z)
    {
        try
        {
            var blockPos = new BlockPos((int)x, 0, (int)z);
            if (!sapi.World.BlockAccessor.IsValidPos(blockPos))
                return null;

            var groundY = sapi.World.BlockAccessor.GetTerrainMapheightAt(blockPos);
            return groundY;
        }
        catch (Exception ex)
        {
            DebugLogger.Error("Failed to find ground level", ex);
            return null;
        }
    }

    /// <summary>
    /// Check if a position is valid for spawning
    /// </summary>
    private bool IsValidSpawnPosition(Vec3d pos)
    {
        var blockPos = new BlockPos((int)pos.X, (int)pos.Y, (int)pos.Z);

        // Check if position is in loaded chunk
        if (!sapi.World.BlockAccessor.IsValidPos(blockPos))
            return false;

        // Check if spawn location is clear (air blocks)
        for (int y = 0; y < 2; y++) // Check 2 blocks high
        {
            var checkPos = blockPos.AddCopy(0, y, 0);
            var block = sapi.World.BlockAccessor.GetBlock(checkPos);

            if (block.Id != 0) // Not air
            {
                return false;
            }
        }

        // Check if there's solid ground below
        var groundPos = blockPos.AddCopy(0, -1, 0);
        var groundBlock = sapi.World.BlockAccessor.GetBlock(groundPos);

        return groundBlock.SideSolid[BlockFacing.UP.Index];
    }

    /// <summary>
    /// Create an entity at the specified position
    /// </summary>
    private Entity? CreateEntity(string entityCode, Vec3d spawnPos, Vec3d playerPos)
    {
        try
        {
            // Try different entity code formats
            var possibleCodes = new[]
            {
                entityCode,
                $"game:{entityCode}",
                entityCode.Replace("game:", ""),
                "game:drifter-normal",
                "game:drifter-deep",
                "game:drifter-corrupt",
                "game:locust"
            };

            EntityProperties? entityType = null;
            string? workingCode = null;

            foreach (var code in possibleCodes)
            {
                try
                {
                    entityType = sapi.World.GetEntityType(new AssetLocation(code));
                    if (entityType != null)
                    {
                        workingCode = code;
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (entityType == null)
            {
                DebugLogger.Error($"Unable to find any valid entity type from: {entityCode}. Tried: {string.Join(", ", possibleCodes)}");
                return null;
            }

            DebugLogger.Event("Entity type found", $"Using: {workingCode} for requested: {entityCode}");

            var entity = sapi.World.ClassRegistry.CreateEntity(entityType);
            if (entity == null)
            {
                DebugLogger.Error($"Failed to create entity: {entityCode}");
                return null;
            }

            // Set position
            entity.ServerPos.SetPos(spawnPos);
            entity.Pos.SetFrom(entity.ServerPos);

            // Store target information for AI
            entity.WatchedAttributes.SetDouble("targetX", playerPos.X);
            entity.WatchedAttributes.SetDouble("targetY", playerPos.Y);
            entity.WatchedAttributes.SetDouble("targetZ", playerPos.Z);
            entity.WatchedAttributes.SetString("spawningSystem", "NewHordeAI");

            // Spawn the entity
            sapi.World.SpawnEntity(entity);

            // Add enhanced waypoint-based AI behavior after spawning
            try
            {
                // For now, use the enhanced AI behavior that integrates waypoints with better physics
                // This combines the best of both approaches: waypoint planning with VS-compatible movement
                var enhancedAI = new AIBehavior(entity, doorHealthManager);
                entity.AddBehavior(enhancedAI);

                DebugLogger.AIEvent("Enhanced AI Behavior added",
                    $"Entity #{entity.EntityId} now has physics-compliant AI with waypoint concepts",
                    entity.EntityId.ToString());
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to add enhanced AI behavior to entity {entityCode}", ex);
            }

            return entity;
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"Failed to create entity {entityCode}", ex);
            return null;
        }
    }
}

/// <summary>
/// Configuration for spawning entities
/// </summary>
public class SpawnConfig
{
    public string EntityCode { get; set; } = "game:drifter-normal";
    public int EntityCount { get; set; } = 5;
    public float MinRadius { get; set; } = 20f;
    public float MaxRadius { get; set; } = 50f;

    public static SpawnConfig Default => new()
    {
        EntityCode = "game:drifter-normal",
        EntityCount = 5,
        MinRadius = 20f,
        MaxRadius = 50f
    };
}
