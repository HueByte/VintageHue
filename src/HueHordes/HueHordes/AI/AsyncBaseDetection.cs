using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HueHordes.Models;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Asynchronous version of base detection with concurrent processing capabilities
/// </summary>
public class AsyncBaseDetection
{
    private readonly ICoreServerAPI sapi;
    private readonly ConcurrentDictionary<string, PlayerBase> detectedBases = new();
    private readonly SemaphoreSlim detectionSemaphore = new(3); // Limit concurrent detections

    // Cache for expensive operations
    private readonly ConcurrentDictionary<string, bool> blockTypeCache = new();
    private readonly ConcurrentDictionary<BlockPos, Block> blockCache = new();

    public AsyncBaseDetection(ICoreServerAPI serverApi)
    {
        sapi = serverApi;
    }

    /// <summary>
    /// Asynchronously detect or update a player base around their current position
    /// </summary>
    public async Task<PlayerBase?> DetectPlayerBaseAsync(IServerPlayer player, int scanRadius = 50, CancellationToken cancellationToken = default)
    {
        var playerPos = player.Entity.ServerPos.XYZ;
        var playerUID = player.PlayerUID;

        // Check if we already have a recent base for this player
        if (detectedBases.TryGetValue(playerUID, out var existingBase))
        {
            var daysSinceUpdate = sapi.World.Calendar.TotalDays - existingBase.LastDetectedDay;

            // If player hasn't moved much and base was detected recently, return cached
            if (playerPos.SquareDistanceTo(existingBase.Center) < 100 * 100 && daysSinceUpdate < 0.5)
            {
                existingBase.LastDetectedDay = sapi.World.Calendar.TotalDays;
                return existingBase;
            }
        }

        // Acquire semaphore to limit concurrent detections
        await detectionSemaphore.WaitAsync(cancellationToken);

        try
        {
            // Perform new base detection asynchronously
            var newBase = await ScanForPlayerBaseAsync(player, scanRadius, cancellationToken);
            if (newBase != null)
            {
                detectedBases[playerUID] = newBase;
            }

            return newBase;
        }
        finally
        {
            detectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Get cached player base (synchronous for performance)
    /// </summary>
    public PlayerBase? GetPlayerBase(string playerUID)
    {
        detectedBases.TryGetValue(playerUID, out var playerBase);
        return playerBase;
    }

    /// <summary>
    /// Asynchronously scan area around player to detect base structure
    /// </summary>
    private async Task<PlayerBase?> ScanForPlayerBaseAsync(IServerPlayer player, int radius, CancellationToken cancellationToken)
    {
        var playerPos = player.Entity.ServerPos.XYZ;

        // Look for bed first as base center (async)
        var bedPosition = await FindNearbyBedAsync(playerPos, radius, cancellationToken);
        var baseCenter = bedPosition ?? playerPos;

        // Scan for artificial structures concurrently
        var artificialBlocks = await ScanForArtificialBlocksAsync(baseCenter, radius, cancellationToken);

        if (artificialBlocks.Count < 10) // Need at least some built blocks
        {
            // Create simple base around player/bed position
            return CreateSimpleBase(player, baseCenter, bedPosition);
        }

        // Analyze structure components concurrently
        var analysisTask = AnalyzeBaseStructureAsync(artificialBlocks, cancellationToken);
        var boundingBox = CalculateBoundingBox(artificialBlocks);

        var (wallBlocks, entrances, interiorArea) = await analysisTask;

        var playerBase = new PlayerBase
        {
            PlayerUID = player.PlayerUID,
            Center = baseCenter,
            BoundingBox = boundingBox,
            Entrances = entrances,
            WallBlocks = wallBlocks,
            InteriorArea = interiorArea,
            LastDetectedDay = sapi.World.Calendar.TotalDays,
            BedPosition = bedPosition,
            HasEnclosure = wallBlocks.Count > 0 && entrances.Count > 0,
            Type = DetermineBaseType(wallBlocks, entrances, interiorArea),
            DetectionConfidence = CalculateDetectionConfidence(artificialBlocks, wallBlocks, entrances)
        };

        return playerBase;
    }

    /// <summary>
    /// Asynchronously find nearby bed that could serve as base center
    /// </summary>
    private async Task<Vec3d?> FindNearbyBedAsync(Vec3d centerPos, int radius, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var blockAccessor = sapi.World.BlockAccessor;

            // Use Parallel.For for concurrent scanning
            object lockObject = new object();
            Vec3d? foundBed = null;

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            try
            {
                Parallel.For(-radius, radius + 1, parallelOptions, x =>
                {
                    if (foundBed != null) return; // Early exit if bed found

                    for (int y = -10; y <= 10; y++)
                    {
                        for (int z = -radius; z <= radius; z++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (foundBed != null) return; // Early exit

                            var pos = new BlockPos((int)(centerPos.X + x), (int)(centerPos.Y + y), (int)(centerPos.Z + z));

                            // Use cached block lookup
                            var block = GetBlockCached(pos);
                            if (block?.Code?.Path?.Contains("bed") == true)
                            {
                                lock (lockObject)
                                {
                                    if (foundBed == null)
                                    {
                                        foundBed = new Vec3d(pos.X, pos.Y, pos.Z);
                                    }
                                }
                            }
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                return null;
            }

            return foundBed;
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously scan for blocks that appear to be player-built
    /// </summary>
    private async Task<List<Vec3d>> ScanForArtificialBlocksAsync(Vec3d center, int radius, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var artificialBlocks = new ConcurrentBag<Vec3d>();

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount / 2 // Don't overwhelm the system
            };

            try
            {
                Parallel.For(-radius, radius + 1, parallelOptions, x =>
                {
                    for (int y = -10; y <= 10; y++)
                    {
                        for (int z = -radius; z <= radius; z++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var pos = new BlockPos((int)(center.X + x), (int)(center.Y + y), (int)(center.Z + z));
                            var block = GetBlockCached(pos);

                            if (IsArtificialBlockCached(block))
                            {
                                artificialBlocks.Add(new Vec3d(pos.X, pos.Y, pos.Z));
                            }
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Return partial results
            }

            return artificialBlocks.ToList();
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously analyze base structure components
    /// </summary>
    private async Task<(List<Vec3d> wallBlocks, List<Vec3d> entrances, List<Vec3d> interiorArea)> AnalyzeBaseStructureAsync(
        List<Vec3d> artificialBlocks, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var boundingBox = CalculateBoundingBox(artificialBlocks);

            // Process components concurrently
            var wallTask = Task.Run(() => IdentifyWallBlocks(artificialBlocks, boundingBox), cancellationToken);

            var wallBlocks = wallTask.Result;
            var entrances = DetectEntrances(wallBlocks, boundingBox);
            var interiorArea = CalculateInteriorArea(boundingBox, wallBlocks, entrances);

            return (wallBlocks, entrances, interiorArea);
        }, cancellationToken);
    }

    /// <summary>
    /// Cached block access to reduce world accessor calls
    /// </summary>
    private Block GetBlockCached(BlockPos pos)
    {
        var key = pos;
        if (blockCache.TryGetValue(key, out var cachedBlock))
        {
            return cachedBlock;
        }

        var block = sapi.World.BlockAccessor.GetBlock(pos);

        // Only cache for a short time to avoid memory issues
        if (blockCache.Count < 10000)
        {
            blockCache[key] = block;
        }

        return block;
    }

    /// <summary>
    /// Cached artificial block determination
    /// </summary>
    private bool IsArtificialBlockCached(Block? block)
    {
        if (block?.Code?.Path == null) return false;

        var path = block.Code.Path;
        if (blockTypeCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var isArtificial = IsArtificialBlock(block);
        blockTypeCache[path] = isArtificial;
        return isArtificial;
    }

    /// <summary>
    /// Determine if a block is likely player-placed
    /// </summary>
    private bool IsArtificialBlock(Block block)
    {
        if (block?.Code?.Path == null) return false;

        var path = block.Code.Path;

        // Common building materials
        if (path.Contains("planks") || path.Contains("stone") || path.Contains("brick") ||
            path.Contains("log") || path.Contains("clay") || path.Contains("thatch"))
            return true;

        // Crafted blocks
        if (path.Contains("door") || path.Contains("window") || path.Contains("stairs") ||
            path.Contains("slab") || path.Contains("fence") || path.Contains("chest"))
            return true;

        // Exclude natural blocks
        if (path.Contains("soil") || path.Contains("grass") || path.Contains("dirt") ||
            path.Contains("gravel") || path.Contains("sand") || path.Contains("water"))
            return false;

        // Check block hardness - artificial blocks tend to be harder
        return block.Resistance > 3.0f;
    }

    /// <summary>
    /// Calculate bounding box for a set of blocks
    /// </summary>
    private Cuboidf CalculateBoundingBox(List<Vec3d> blocks)
    {
        if (blocks.Count == 0)
            return new Cuboidf(0, 0, 0, 1, 1, 1);

        float minX = (float)blocks.Min(b => b.X);
        float minY = (float)blocks.Min(b => b.Y);
        float minZ = (float)blocks.Min(b => b.Z);
        float maxX = (float)blocks.Max(b => b.X);
        float maxY = (float)blocks.Max(b => b.Y);
        float maxZ = (float)blocks.Max(b => b.Z);

        return new Cuboidf(minX, minY, minZ, maxX + 1, maxY + 1, maxZ + 1);
    }

    /// <summary>
    /// Identify blocks that form walls/perimeter (can be parallelized)
    /// </summary>
    private List<Vec3d> IdentifyWallBlocks(List<Vec3d> artificialBlocks, Cuboidf boundingBox)
    {
        return artificialBlocks.AsParallel()
            .Where(block =>
                Math.Abs(block.X - boundingBox.MinX) < 2 ||
                Math.Abs(block.X - boundingBox.MaxX) < 2 ||
                Math.Abs(block.Z - boundingBox.MinZ) < 2 ||
                Math.Abs(block.Z - boundingBox.MaxZ) < 2)
            .ToList();
    }

    /// <summary>
    /// Detect entrance points in the base
    /// </summary>
    private List<Vec3d> DetectEntrances(List<Vec3d> wallBlocks, Cuboidf boundingBox)
    {
        var entrances = new List<Vec3d>();

        // Simplified entrance detection - can be enhanced
        if (entrances.Count == 0)
        {
            var centerPos = new Vec3d((boundingBox.MinX + boundingBox.MaxX) / 2, boundingBox.MinY, (boundingBox.MinZ + boundingBox.MaxZ) / 2);
            var radiusX = (boundingBox.MaxX - boundingBox.MinX) / 2;

            var entrance = centerPos + new Vec3d(radiusX, 0, 0);
            entrances.Add(entrance);
        }

        return entrances;
    }

    /// <summary>
    /// Calculate interior area for patrol purposes
    /// </summary>
    private List<Vec3d> CalculateInteriorArea(Cuboidf boundingBox, List<Vec3d> wallBlocks, List<Vec3d> entrances)
    {
        var interiorArea = new List<Vec3d>();

        // Use parallel processing for interior calculation
        var candidates = new ConcurrentBag<Vec3d>();

        Parallel.For((int)boundingBox.MinX, (int)boundingBox.MaxX, x =>
        {
            for (int z = (int)boundingBox.MinZ; z < boundingBox.MaxZ; z += 2)
            {
                var point = new Vec3d(x, boundingBox.MinY, z);

                // Skip if too close to wall blocks
                bool tooCloseToWall = wallBlocks.Any(wall => point.SquareDistanceTo(wall) < 4);
                if (!tooCloseToWall)
                {
                    candidates.Add(point);
                }
            }
        });

        return candidates.ToList();
    }

    /// <summary>
    /// Create a simple base when no complex structure is detected
    /// </summary>
    private PlayerBase CreateSimpleBase(IServerPlayer player, Vec3d center, Vec3d? bedPosition)
    {
        var simpleRadius = 15f;
        var boundingBox = new Cuboidf(
            (float)center.X - simpleRadius, (float)center.Y - 5, (float)center.Z - simpleRadius,
            (float)center.X + simpleRadius, (float)center.Y + 5, (float)center.Z + simpleRadius);

        return new PlayerBase
        {
            PlayerUID = player.PlayerUID,
            Center = center,
            BoundingBox = boundingBox,
            Entrances = new List<Vec3d> { center },
            WallBlocks = new List<Vec3d>(),
            InteriorArea = new List<Vec3d> { center },
            LastDetectedDay = sapi.World.Calendar.TotalDays,
            BedPosition = bedPosition,
            HasEnclosure = false,
            Type = BaseType.Simple,
            DetectionConfidence = 0.3f
        };
    }

    /// <summary>
    /// Determine the type of base based on detected features
    /// </summary>
    private BaseType DetermineBaseType(List<Vec3d> wallBlocks, List<Vec3d> entrances, List<Vec3d> interiorArea)
    {
        if (wallBlocks.Count > 50 && entrances.Count >= 1)
            return BaseType.Walled;

        if (interiorArea.Count > 100)
            return BaseType.Complex;

        return BaseType.Simple;
    }

    /// <summary>
    /// Calculate confidence in the base detection
    /// </summary>
    private float CalculateDetectionConfidence(List<Vec3d> artificialBlocks, List<Vec3d> wallBlocks, List<Vec3d> entrances)
    {
        float confidence = 0.2f; // Base confidence

        confidence += Math.Min(0.4f, artificialBlocks.Count / 100.0f);

        if (wallBlocks.Count > 10)
            confidence += 0.3f;

        confidence += entrances.Count * 0.1f;

        return Math.Min(1.0f, confidence);
    }

    /// <summary>
    /// Clear caches to prevent memory leaks
    /// </summary>
    public void ClearCaches()
    {
        blockCache.Clear();
        // Keep blockTypeCache as it's small and useful
    }

    /// <summary>
    /// Get detection statistics
    /// </summary>
    public async Task<string> GetDetectionStatsAsync()
    {
        return await Task.Run(() =>
        {
            var stats = $"Async Base Detection Stats:\n" +
                       $"Cached Bases: {detectedBases.Count}\n" +
                       $"Block Cache: {blockCache.Count} entries\n" +
                       $"Block Type Cache: {blockTypeCache.Count} entries\n" +
                       $"Available Semaphore Slots: {detectionSemaphore.CurrentCount}/3";
            return stats;
        });
    }
}