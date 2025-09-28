using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using HueHordes.Behaviors;
using HueHordes.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Asynchronous main coordinator for the horde AI system with concurrent processing
/// </summary>
public class AsyncHordeAI : IDisposable
{
    private readonly ICoreServerAPI sapi;
    private readonly AsyncBaseDetection baseDetection;
    private readonly AsyncSmartTargeting smartTargeting;

    // Concurrent processing infrastructure
    private readonly SemaphoreSlim spawningSemaphore = new(3); // Limit concurrent spawning operations
    private readonly ConcurrentQueue<SpawnRequest> spawnQueue = new();
    private readonly Channel<EntityProcessingRequest> entityChannel;
    private readonly ChannelWriter<EntityProcessingRequest> entityWriter;
    private readonly ChannelReader<EntityProcessingRequest> entityReader;

    private readonly Timer cacheCleanupTimer;
    private readonly CancellationTokenSource cancellationTokenSource = new();

    // Performance tracking
    private readonly ConcurrentDictionary<string, long> performanceMetrics = new();

    private static AsyncHordeAI? instance;
    public static AsyncHordeAI? Instance => instance;

    public AsyncBaseDetection BaseDetection => baseDetection;
    public AsyncSmartTargeting SmartTargeting => smartTargeting;

    public AsyncHordeAI(ICoreServerAPI serverApi)
    {
        sapi = serverApi;
        baseDetection = new AsyncBaseDetection(serverApi);
        smartTargeting = new AsyncSmartTargeting(serverApi, baseDetection);

        instance = this;

        // Create bounded channel for entity processing
        var channelOptions = new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        entityChannel = Channel.CreateBounded<EntityProcessingRequest>(channelOptions);
        entityWriter = entityChannel.Writer;
        entityReader = entityChannel.Reader;

        // Start background entity processor
        _ = Task.Run(ProcessEntityRequestsAsync, cancellationTokenSource.Token);

        // Start cache cleanup timer (every 30 seconds)
        cacheCleanupTimer = new Timer(CleanupCaches, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        sapi.Logger.Debug("[AsyncHordeAI] Asynchronous AI system initialized");
    }

    /// <summary>
    /// Asynchronously calculate smart spawn positions outside player base
    /// </summary>
    public async Task<List<Vec3d>> CalculateSpawnPositionsAsync(IServerPlayer player, int count, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Detect or get the player's base asynchronously
            var playerBase = await baseDetection.DetectPlayerBaseAsync(player, 50, cancellationToken);

            if (playerBase != null)
            {
                // Get spawn positions outside the base
                var spawnPositions = playerBase.GetSafeSpawnPositions(count);

                // Adjust for terrain height concurrently
                var adjustedPositions = await AdjustPositionsForTerrainAsync(spawnPositions, cancellationToken);
                return adjustedPositions;
            }
            else
            {
                // Fallback to traditional ring spawning around player
                return await CalculateRingSpawnPositionsAsync(player.Entity.ServerPos.XYZ, count, 15f, 30f, cancellationToken);
            }
        }
        finally
        {
            RecordPerformanceMetric("CalculateSpawnPositions", DateTime.UtcNow.Subtract(startTime).Ticks);
        }
    }

    /// <summary>
    /// Asynchronously spawn a horde with advanced AI for a specific player
    /// </summary>
    public async Task SpawnSmartHordeAsync(IServerPlayer player, ServerConfig config, double currentDay, CancellationToken cancellationToken = default)
    {
        await spawningSemaphore.WaitAsync(cancellationToken);

        try
        {
            var startTime = DateTime.UtcNow;

            // Calculate spawn positions
            var spawnPositions = await CalculateSpawnPositionsAsync(player, config.Count, cancellationToken);
            var initialPlayerPos = player.Entity.ServerPos.XYZ.Clone();

            sapi.Logger.Debug($"[AsyncHordeAI] Spawning smart horde for {player.PlayerName} with {spawnPositions.Count} entities");

            // Create spawn requests
            var spawnRequests = new List<SpawnRequest>();
            for (int i = 0; i < config.Count && i < spawnPositions.Count; i++)
            {
                var entityCode = config.EntityCodes[i % config.EntityCodes.Length];
                var spawnPos = spawnPositions[i];

                spawnRequests.Add(new SpawnRequest
                {
                    EntityCode = entityCode,
                    SpawnPosition = spawnPos,
                    TargetPlayer = player,
                    Config = config,
                    CurrentDay = currentDay
                });
            }

            // Process spawn requests concurrently
            var spawnTasks = spawnRequests.Select(request =>
                TrySpawnSmartEntityAsync(request, cancellationToken)).ToList();

            await Task.WhenAll(spawnTasks);

            var successfulSpawns = spawnTasks.Count(t => t.Result);
            sapi.Logger.Debug($"[AsyncHordeAI] Successfully spawned {successfulSpawns}/{config.Count} entities");

            RecordPerformanceMetric("SpawnSmartHorde", DateTime.UtcNow.Subtract(startTime).Ticks);
        }
        finally
        {
            spawningSemaphore.Release();
        }
    }

    /// <summary>
    /// Asynchronously spawn a single entity with smart AI behavior
    /// </summary>
    private async Task<bool> TrySpawnSmartEntityAsync(SpawnRequest request, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                var loc = new AssetLocation(request.EntityCode);
                if (string.IsNullOrEmpty(loc.Domain))
                    loc = new AssetLocation("game", loc.Path);

                var etype = sapi.World.GetEntityType(loc);
                if (etype == null)
                {
                    sapi.Logger.Warning($"[AsyncHordeAI] Unknown entity '{request.EntityCode}'");
                    return false;
                }

                var entity = sapi.World.ClassRegistry.CreateEntity(etype);
                entity.ServerPos.SetPos(request.SpawnPosition);
                entity.Pos.SetFrom(entity.ServerPos);

                // Spawn the entity first
                sapi.World.SpawnEntity(entity);

                // Set up smart AI attributes
                entity.WatchedAttributes.SetString("hordeTargetPlayerUID", request.TargetPlayer.PlayerUID);
                entity.WatchedAttributes.SetDouble("hordeOriginalTargetX", request.TargetPlayer.Entity.ServerPos.X);
                entity.WatchedAttributes.SetDouble("hordeOriginalTargetY", request.TargetPlayer.Entity.ServerPos.Y);
                entity.WatchedAttributes.SetDouble("hordeOriginalTargetZ", request.TargetPlayer.Entity.ServerPos.Z);
                entity.WatchedAttributes.SetFloat("hordeBehaviorDuration", request.Config.NudgeSeconds * 3);
                entity.WatchedAttributes.SetFloat("hordeSpeed", request.Config.NudgeSpeed * 1.5f);

                // Add smart behavior
                if (request.Config.NudgeTowardInitialPos && entity is EntityAgent)
                {
                    try
                    {
                        var smartBehavior = new AsyncSmartHordeBehavior(entity);
                        entity.AddBehavior(smartBehavior);

                        // Queue entity for async processing
                        _ = QueueEntityProcessingAsync(entity, request.TargetPlayer.PlayerUID);

                        return true;
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning($"[AsyncHordeAI] Failed to add smart behavior: {ex.Message}");

                        // Fallback to simple behavior
                        try
                        {
                            var fallbackBehavior = new HordeNudgeBehavior(entity);
                            entity.AddBehavior(fallbackBehavior);
                            return true;
                        }
                        catch (Exception fallbackEx)
                        {
                            sapi.Logger.Error($"[AsyncHordeAI] Fallback behavior also failed: {fallbackEx.Message}");
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[AsyncHordeAI] Failed to spawn entity {request.EntityCode}: {ex.Message}");
                return false;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Queue entity for asynchronous processing
    /// </summary>
    private async Task QueueEntityProcessingAsync(Entity entity, string targetPlayerUID)
    {
        var request = new EntityProcessingRequest
        {
            EntityId = entity.EntityId,
            TargetPlayerUID = targetPlayerUID,
            QueueTime = DateTime.UtcNow
        };

        await entityWriter.WriteAsync(request, cancellationTokenSource.Token);
    }

    /// <summary>
    /// Background processor for entity requests
    /// </summary>
    private async Task ProcessEntityRequestsAsync()
    {
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);

        await foreach (var request in entityReader.ReadAllAsync(cancellationTokenSource.Token))
        {
            _ = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationTokenSource.Token);
                try
                {
                    await ProcessEntityRequestAsync(request);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationTokenSource.Token);
        }
    }

    /// <summary>
    /// Process individual entity request
    /// </summary>
    private async Task ProcessEntityRequestAsync(EntityProcessingRequest request)
    {
        try
        {
            var entity = sapi.World.GetEntityById(request.EntityId);
            if (entity?.Alive != true) return;

            // Perform periodic AI updates for this entity
            // This could include pathfinding, target validation, etc.
            await Task.Delay(100, cancellationTokenSource.Token); // Placeholder for actual processing

            // Re-queue if entity is still alive (simple keep-alive mechanism)
            if (entity.Alive)
            {
                await Task.Delay(5000, cancellationTokenSource.Token); // Wait 5 seconds
                await QueueEntityProcessingAsync(entity, request.TargetPlayerUID);
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[AsyncHordeAI] Error processing entity request: {ex.Message}");
        }
    }

    /// <summary>
    /// Asynchronously adjust spawn positions for terrain height
    /// </summary>
    private async Task<List<Vec3d>> AdjustPositionsForTerrainAsync(List<Vec3d> positions, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            return positions.AsParallel()
                .WithCancellation(cancellationToken)
                .Select(pos =>
                {
                    var terrainHeight = GetTerrainHeight(pos);
                    return new Vec3d(pos.X, terrainHeight + 1, pos.Z);
                })
                .ToList();
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously calculate ring spawn positions (fallback method)
    /// </summary>
    private async Task<List<Vec3d>> CalculateRingSpawnPositionsAsync(Vec3d center, int count, float minRadius, float maxRadius, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var positions = new List<Vec3d>();
            var rand = new Random();

            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var angle = (2 * Math.PI * i) / count + (rand.NextDouble() - 0.5) * 0.5;
                var radius = minRadius + (float)(rand.NextDouble() * (maxRadius - minRadius));

                var x = center.X + Math.Cos(angle) * radius;
                var z = center.Z + Math.Sin(angle) * radius;
                var y = GetTerrainHeight(new Vec3d(x, 0, z)) + 1;

                positions.Add(new Vec3d(x, y, z));
            }

            return positions;
        }, cancellationToken);
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
    /// Get base information for a player
    /// </summary>
    public PlayerBase? GetPlayerBaseInfo(string playerUID)
    {
        return baseDetection.GetPlayerBase(playerUID);
    }

    /// <summary>
    /// Asynchronously force refresh base detection for a player
    /// </summary>
    public async Task<PlayerBase?> RefreshPlayerBaseAsync(IServerPlayer player, CancellationToken cancellationToken = default)
    {
        return await baseDetection.DetectPlayerBaseAsync(player, 60, cancellationToken);
    }

    /// <summary>
    /// Asynchronously get comprehensive AI system statistics
    /// </summary>
    public async Task<string> GetAIStatsAsync()
    {
        var baseStatsTask = baseDetection.GetDetectionStatsAsync();
        var targetingStatsTask = smartTargeting.GetTargetingStatsAsync();

        await Task.WhenAll(baseStatsTask, targetingStatsTask);

        var stats = new System.Text.StringBuilder();
        stats.AppendLine("=== Async Horde AI System Stats ===");
        stats.AppendLine($"System Status: Active");
        stats.AppendLine($"Available Spawn Slots: {spawningSemaphore.CurrentCount}/3");
        stats.AppendLine($"Entity Queue: Active");
        stats.AppendLine();

        stats.AppendLine(await baseStatsTask);
        stats.AppendLine();

        stats.AppendLine(await targetingStatsTask);
        stats.AppendLine();

        // Performance metrics
        stats.AppendLine("=== Performance Metrics ===");
        foreach (var metric in performanceMetrics)
        {
            var avgTicks = metric.Value;
            var avgMs = TimeSpan.FromTicks(avgTicks).TotalMilliseconds;
            stats.AppendLine($"{metric.Key}: {avgMs:F2}ms avg");
        }

        return stats.ToString();
    }

    /// <summary>
    /// Record performance metric
    /// </summary>
    private void RecordPerformanceMetric(string operation, long ticks)
    {
        performanceMetrics.AddOrUpdate(operation, ticks, (key, oldValue) => (oldValue + ticks) / 2);
    }

    /// <summary>
    /// Cleanup expired caches (called by timer)
    /// </summary>
    private void CleanupCaches(object? state)
    {
        try
        {
            baseDetection.ClearCaches();
            smartTargeting.ClearExpiredCache();
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning($"[AsyncHordeAI] Cache cleanup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        cancellationTokenSource.Cancel();

        entityWriter.Complete();
        cacheCleanupTimer?.Dispose();

        smartTargeting.Dispose();
        spawningSemaphore.Dispose();
        cancellationTokenSource.Dispose();

        instance = null;
    }
}

/// <summary>
/// Request object for spawning entities
/// </summary>
internal class SpawnRequest
{
    public string EntityCode { get; set; } = string.Empty;
    public Vec3d SpawnPosition { get; set; } = Vec3d.Zero;
    public IServerPlayer TargetPlayer { get; set; } = null!;
    public ServerConfig Config { get; set; } = null!;
    public double CurrentDay { get; set; }
}

/// <summary>
/// Request object for entity processing
/// </summary>
internal class EntityProcessingRequest
{
    public long EntityId { get; set; }
    public string TargetPlayerUID { get; set; } = string.Empty;
    public DateTime QueueTime { get; set; }
}