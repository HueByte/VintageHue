using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using HueHordes.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Asynchronous version of smart targeting with concurrent processing and caching
/// </summary>
public class AsyncSmartTargeting
{
    private readonly ICoreServerAPI sapi;
    private readonly AsyncBaseDetection baseDetection;

    // Thread-safe caches and collections
    private readonly ConcurrentDictionary<string, List<HordeTarget>> targetCache = new();
    private readonly ConcurrentDictionary<string, DateTime> cacheExpiry = new();
    private readonly SemaphoreSlim targetingSemaphore = new(10); // Allow multiple concurrent targeting operations

    // Channel for batching line-of-sight checks
    private readonly Channel<LineOfSightRequest> losChannel;
    private readonly ChannelWriter<LineOfSightRequest> losWriter;
    private readonly ChannelReader<LineOfSightRequest> losReader;

    private readonly CancellationTokenSource cancellationTokenSource = new();

    public AsyncSmartTargeting(ICoreServerAPI serverApi, AsyncBaseDetection baseDetectionSystem)
    {
        sapi = serverApi;
        baseDetection = baseDetectionSystem;

        // Create bounded channel for line-of-sight requests
        var channelOptions = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        losChannel = Channel.CreateBounded<LineOfSightRequest>(channelOptions);
        losWriter = losChannel.Writer;
        losReader = losChannel.Reader;

        // Start background line-of-sight processor
        _ = Task.Run(ProcessLineOfSightRequestsAsync, cancellationTokenSource.Token);
    }

    /// <summary>
    /// Asynchronously get the best target for an entity from the given position
    /// </summary>
    public async Task<HordeTarget?> GetBestTargetAsync(Vec3d fromPosition, string originalPlayerUID, double maxRange = 100.0, CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cacheKey = $"{originalPlayerUID}:{fromPosition.X:F0}:{fromPosition.Z:F0}";
        if (TryGetCachedTarget(cacheKey, out var cachedTarget))
        {
            return cachedTarget;
        }

        await targetingSemaphore.WaitAsync(cancellationToken);

        try
        {
            var candidates = new List<HordeTarget>();
            var currentTime = sapi.World.Calendar.TotalDays;

            // Process different target types concurrently
            var playerTargetsTask = GetPlayerTargetsAsync(fromPosition, originalPlayerUID, maxRange, currentTime, cancellationToken);
            var baseTargetsTask = GetBaseTargetsAsync(fromPosition, originalPlayerUID, currentTime, cancellationToken);
            var entityTargetsTask = GetEntityTargetsAsync(fromPosition, maxRange, currentTime, cancellationToken);

            await Task.WhenAll(playerTargetsTask, baseTargetsTask, entityTargetsTask);

            candidates.AddRange(await playerTargetsTask);
            candidates.AddRange(await baseTargetsTask);
            candidates.AddRange(await entityTargetsTask);

            // Add patrol targets if no active targets found
            if (candidates.Count == 0 || candidates.All(t => t.Priority < 50))
            {
                var patrolTargets = await GetPatrolTargetsAsync(fromPosition, originalPlayerUID, currentTime, cancellationToken);
                candidates.AddRange(patrolTargets);
            }

            // Select best target concurrently
            var bestTarget = await SelectBestTargetAsync(candidates, fromPosition, cancellationToken);

            // Cache result
            if (bestTarget != null)
            {
                CacheTarget(cacheKey, bestTarget);
            }

            return bestTarget;
        }
        finally
        {
            targetingSemaphore.Release();
        }
    }

    /// <summary>
    /// Asynchronously get player targets
    /// </summary>
    private async Task<List<HordeTarget>> GetPlayerTargetsAsync(Vec3d fromPosition, string originalPlayerUID, double maxRange, double currentTime, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var targets = new List<HordeTarget>();
            var maxRangeSquared = maxRange * maxRange;

            var players = sapi.World.AllOnlinePlayers.OfType<IServerPlayer>()
                .Where(p => p.Entity?.Alive == true)
                .ToList();

            // Process players in parallel
            var playerTargets = new ConcurrentBag<HordeTarget>();

            Parallel.ForEach(players, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, player =>
            {
                var playerPos = player.Entity.ServerPos.XYZ;
                var distanceSquared = fromPosition.SquareDistanceTo(playerPos);

                if (distanceSquared <= maxRangeSquared)
                {
                    var target = new HordeTarget
                    {
                        Position = playerPos.Clone(),
                        Type = TargetType.Player,
                        Priority = player.PlayerUID == originalPlayerUID ? 120 : 100,
                        TargetPlayer = player,
                        LastSeenTime = currentTime,
                        Distance = Math.Sqrt(distanceSquared),
                        IsVisible = true,
                        ValidityDuration = 2000f
                    };

                    playerTargets.Add(target);
                }
            });

            return playerTargets.ToList();
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously get base-related targets
    /// </summary>
    private async Task<List<HordeTarget>> GetBaseTargetsAsync(Vec3d fromPosition, string originalPlayerUID, double currentTime, CancellationToken cancellationToken)
    {
        var playerBase = baseDetection.GetPlayerBase(originalPlayerUID);
        if (playerBase == null) return new List<HordeTarget>();

        return await Task.Run(() =>
        {
            var targets = new List<HordeTarget>();

            // Process entrances in parallel
            var entranceTargets = playerBase.Entrances.AsParallel()
                .WithCancellation(cancellationToken)
                .Select(entrance =>
                {
                    var distance = Math.Sqrt(fromPosition.SquareDistanceTo(entrance));
                    return new HordeTarget
                    {
                        Position = entrance.Clone(),
                        Type = TargetType.BaseEntrance,
                        Priority = 60,
                        RelatedBase = playerBase,
                        LastSeenTime = currentTime,
                        Distance = distance,
                        IsVisible = true,
                        ValidityDuration = 5000f
                    };
                })
                .ToList();

            targets.AddRange(entranceTargets);

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
                ValidityDuration = 10000f
            };

            targets.Add(centerTarget);

            return targets;
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously get entity targets
    /// </summary>
    private async Task<List<HordeTarget>> GetEntityTargetsAsync(Vec3d fromPosition, double maxRange, double currentTime, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var maxRangeSquared = maxRange * maxRange;
            var nearbyEntities = sapi.World.GetEntitiesAround(fromPosition, (float)maxRange, (float)maxRange);

            var entityTargets = nearbyEntities.OfType<EntityAgent>()
                .AsParallel()
                .WithCancellation(cancellationToken)
                .Where(entity => entity.Alive && !entity.Code.Path.Contains("drifter"))
                .Select(entity =>
                {
                    var entityPos = entity.ServerPos.XYZ;
                    var distanceSquared = fromPosition.SquareDistanceTo(entityPos);

                    if (distanceSquared <= maxRangeSquared)
                    {
                        return new HordeTarget
                        {
                            Position = entityPos.Clone(),
                            Type = TargetType.Entity,
                            Priority = 80,
                            TargetEntity = entity,
                            LastSeenTime = currentTime,
                            Distance = Math.Sqrt(distanceSquared),
                            IsVisible = true,
                            ValidityDuration = 1500f
                        };
                    }
                    return null;
                })
                .Where(target => target != null)
                .Cast<HordeTarget>()
                .ToList();

            return entityTargets;
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously get patrol targets
    /// </summary>
    private async Task<List<HordeTarget>> GetPatrolTargetsAsync(Vec3d fromPosition, string originalPlayerUID, double currentTime, CancellationToken cancellationToken)
    {
        var playerBase = baseDetection.GetPlayerBase(originalPlayerUID);
        if (playerBase == null) return new List<HordeTarget>();

        return await Task.Run(() =>
        {
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
                ValidityDuration = 15000f,
                CustomData = "patrol"
            };

            return new List<HordeTarget> { target };
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously select the best target from candidates
    /// </summary>
    private async Task<HordeTarget?> SelectBestTargetAsync(List<HordeTarget> candidates, Vec3d fromPosition, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0) return null;

        return await Task.Run(() =>
        {
            // Calculate dynamic priorities in parallel
            Parallel.ForEach(candidates, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, candidate =>
            {
                candidate.Priority = candidate.CalculateDynamicPriority(fromPosition);
            });

            // Sort and return best
            var validCandidates = candidates
                .Where(c => c.IsValid(sapi.World.Calendar.TotalDays))
                .OrderByDescending(c => c.Priority)
                .ThenBy(c => c.Distance)
                .ToList();

            return validCandidates.FirstOrDefault();
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously check line of sight between positions
    /// </summary>
    public async Task<bool> HasLineOfSightAsync(Vec3d fromPosition, Vec3d toPosition, float maxDistance = 50f, CancellationToken cancellationToken = default)
    {
        var distance = fromPosition.DistanceTo(toPosition);
        if (distance > maxDistance) return false;

        var request = new LineOfSightRequest
        {
            FromPosition = fromPosition,
            ToPosition = toPosition,
            MaxDistance = maxDistance,
            CompletionSource = new TaskCompletionSource<bool>()
        };

        // Send request to background processor
        await losWriter.WriteAsync(request, cancellationToken);

        // Wait for result
        return await request.CompletionSource.Task;
    }

    /// <summary>
    /// Background processor for line-of-sight checks
    /// </summary>
    private async Task ProcessLineOfSightRequestsAsync()
    {
        await foreach (var request in losReader.ReadAllAsync(cancellationTokenSource.Token))
        {
            try
            {
                var result = await Task.Run(() =>
                {
                    var blockAccessor = sapi.World.BlockAccessor;
                    var direction = (request.ToPosition - request.FromPosition).Normalize();
                    var stepSize = 1.0;
                    var steps = (int)(request.FromPosition.DistanceTo(request.ToPosition) / stepSize);

                    // Check for blocking terrain between positions
                    for (int i = 1; i < steps; i++)
                    {
                        var checkPos = request.FromPosition + direction * (i * stepSize);
                        var blockPos = new BlockPos((int)checkPos.X, (int)checkPos.Y + 1, (int)checkPos.Z);

                        var block = blockAccessor.GetBlock(blockPos);
                        if (block.Code.Path != "air" && block.CollisionBoxes?.Length > 0)
                        {
                            return false; // Line of sight blocked
                        }
                    }

                    return true;
                }, cancellationTokenSource.Token);

                request.CompletionSource.SetResult(result);
            }
            catch (Exception ex)
            {
                request.CompletionSource.SetException(ex);
            }
        }
    }

    /// <summary>
    /// Asynchronously update target information
    /// </summary>
    public async Task UpdateTargetAsync(HordeTarget target, CancellationToken cancellationToken = default)
    {
        if (target == null) return;

        await Task.Run(() =>
        {
            var currentTime = sapi.World.Calendar.TotalDays;
            target.UpdatePosition(currentTime);

            // Update visibility for player/entity targets
            if (target.Type == TargetType.Player || target.Type == TargetType.Entity)
            {
                target.IsVisible = target.IsValid(currentTime);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously get fallback target when current target is lost
    /// </summary>
    public async Task<HordeTarget?> GetFallbackTargetAsync(Vec3d lastKnownPosition, string originalPlayerUID, HordeTarget? lostTarget, CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            // Try to get any nearby player first
            var nearbyTarget = await GetBestTargetAsync(lastKnownPosition, originalPlayerUID, 30.0, cancellationToken);
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
        }, cancellationToken);
    }

    /// <summary>
    /// Try to get a cached target
    /// </summary>
    private bool TryGetCachedTarget(string cacheKey, out HordeTarget? target)
    {
        target = null;

        if (!targetCache.TryGetValue(cacheKey, out var cachedTargets) ||
            !cacheExpiry.TryGetValue(cacheKey, out var expiry) ||
            DateTime.UtcNow > expiry)
        {
            return false;
        }

        target = cachedTargets.FirstOrDefault();
        return target != null;
    }

    /// <summary>
    /// Cache a target for performance
    /// </summary>
    private void CacheTarget(string cacheKey, HordeTarget target)
    {
        targetCache[cacheKey] = new List<HordeTarget> { target };
        cacheExpiry[cacheKey] = DateTime.UtcNow.AddSeconds(2); // Short cache duration
    }

    /// <summary>
    /// Clear expired cache entries
    /// </summary>
    public void ClearExpiredCache()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = cacheExpiry.Where(kvp => kvp.Value < now).Select(kvp => kvp.Key).ToList();

        foreach (var key in expiredKeys)
        {
            targetCache.TryRemove(key, out _);
            cacheExpiry.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Get async targeting statistics
    /// </summary>
    public async Task<string> GetTargetingStatsAsync()
    {
        return await Task.Run(() =>
        {
            var stats = $"Async Smart Targeting Stats:\n" +
                       $"Target Cache: {targetCache.Count} entries\n" +
                       $"Available Semaphore Slots: {targetingSemaphore.CurrentCount}/10\n" +
                       $"LOS Channel: Active";
            return stats;
        });
    }

    /// <summary>
    /// Dispose of resources
    /// </summary>
    public void Dispose()
    {
        cancellationTokenSource.Cancel();
        losWriter.Complete();
        targetingSemaphore.Dispose();
        cancellationTokenSource.Dispose();
    }
}

/// <summary>
/// Request object for batched line-of-sight checks
/// </summary>
internal class LineOfSightRequest
{
    public Vec3d FromPosition { get; set; } = Vec3d.Zero;
    public Vec3d ToPosition { get; set; } = Vec3d.Zero;
    public float MaxDistance { get; set; }
    public TaskCompletionSource<bool> CompletionSource { get; set; } = new();
}