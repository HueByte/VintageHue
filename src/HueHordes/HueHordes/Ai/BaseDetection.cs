using System;
using System.Collections.Generic;
using System.Linq;
using HueHordes.Debug;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Base detection system that finds player bases by looking for key structures
/// Step 2: Basic base detection functionality
/// </summary>
public class BaseDetection
{
    private readonly ICoreServerAPI sapi;
    private readonly Dictionary<string, DetectedBase> playerBases = new();

    public BaseDetection(ICoreServerAPI serverApi)
    {
        sapi = serverApi ?? throw new ArgumentNullException(nameof(serverApi));
    }

    /// <summary>
    /// Detect a player's base location
    /// </summary>
    public DetectedBase? DetectPlayerBase(IServerPlayer player, int searchRadius = 50)
    {
        if (player?.Entity?.ServerPos == null)
        {
            DebugLogger.AIEvent("Base detection failed", "Player or position is null", "BaseDetection");
            return null;
        }

        var playerPos = player.Entity.ServerPos.XYZ;
        var playerUID = player.PlayerUID;

        DebugLogger.AIEvent("Starting base detection",
            $"Player: {player.PlayerName}, Radius: {searchRadius}",
            "BaseDetection");

        // Check if we already have a recent detection
        if (playerBases.TryGetValue(playerUID, out var existingBase))
        {
            if (DateTime.UtcNow.Subtract(existingBase.DetectedAt).TotalMinutes < 5) // Cache for 5 minutes
            {
                DebugLogger.AIEvent("Using cached base",
                    $"Age: {DateTime.UtcNow.Subtract(existingBase.DetectedAt).TotalMinutes:F1} minutes",
                    "BaseDetection");
                return existingBase;
            }
        }

        var detectedBase = ScanForBase(playerPos, searchRadius);

        if (detectedBase != null)
        {
            detectedBase.PlayerUID = playerUID;
            detectedBase.PlayerName = player.PlayerName;
            playerBases[playerUID] = detectedBase;

            DebugLogger.AIEvent("Base detected",
                $"Center: {detectedBase.Center.X:F1},{detectedBase.Center.Y:F1},{detectedBase.Center.Z:F1}, " +
                $"Score: {detectedBase.BaseScore}, Type: {detectedBase.BaseType}",
                "BaseDetection");
        }
        else
        {
            DebugLogger.AIEvent("No base found",
                $"Scanned {searchRadius}x{searchRadius} area around player",
                "BaseDetection");
        }

        return detectedBase;
    }

    /// <summary>
    /// Scan area around player position for base indicators
    /// </summary>
    private DetectedBase? ScanForBase(Vec3d playerPos, int radius)
    {
        var baseIndicators = new List<BaseIndicator>();

        var startX = (int)(playerPos.X - radius);
        var endX = (int)(playerPos.X + radius);
        var startZ = (int)(playerPos.Z - radius);
        var endZ = (int)(playerPos.Z + radius);

        var minY = Math.Max(0, (int)(playerPos.Y - 20));
        var maxY = Math.Min(255, (int)(playerPos.Y + 20));

        // Scan the area for base indicators
        for (int x = startX; x <= endX; x += 4) // Sample every 4 blocks for performance
        {
            for (int z = startZ; z <= endZ; z += 4)
            {
                for (int y = minY; y <= maxY; y += 2)
                {
                    var blockPos = new BlockPos(x, y, z);
                    if (!sapi.World.BlockAccessor.IsValidPos(blockPos))
                        continue;

                    var block = sapi.World.BlockAccessor.GetBlock(blockPos);
                    if (block.Id == 0) continue; // Skip air blocks

                    var indicator = ClassifyBlock(block, blockPos.ToVec3d());
                    if (indicator != null)
                    {
                        baseIndicators.Add(indicator);
                        DebugLogger.Event($"Found {indicator.Type} indicator",
                            $"Position: {indicator.Position.X:F1},{indicator.Position.Y:F1},{indicator.Position.Z:F1}, Block: {block.Code}");
                    }
                }
            }
        }

        if (baseIndicators.Count == 0)
            return null;

        return AnalyzeIndicators(baseIndicators, playerPos);
    }

    /// <summary>
    /// Classify a block as a base indicator
    /// </summary>
    private BaseIndicator? ClassifyBlock(Block block, Vec3d position)
    {
        var blockCode = block.Code?.ToString();
        if (blockCode == null) return null;

        // Doors (high priority - clear base indicator)
        if (blockCode.Contains("door", StringComparison.OrdinalIgnoreCase))
        {
            return new BaseIndicator
            {
                Position = position,
                Type = BaseIndicatorType.Door,
                Priority = 10,
                Range = 15f
            };
        }

        // Beds (high priority - spawn point)
        if (blockCode.Contains("bed", StringComparison.OrdinalIgnoreCase))
        {
            return new BaseIndicator
            {
                Position = position,
                Type = BaseIndicatorType.Bed,
                Priority = 10,
                Range = 20f
            };
        }

        // Chests (medium priority - storage)
        if (blockCode.Contains("chest", StringComparison.OrdinalIgnoreCase))
        {
            return new BaseIndicator
            {
                Position = position,
                Type = BaseIndicatorType.Storage,
                Priority = 6,
                Range = 12f
            };
        }

        // Workbenches/Crafting (medium priority)
        if (blockCode.Contains("workbench", StringComparison.OrdinalIgnoreCase) ||
            blockCode.Contains("anvil", StringComparison.OrdinalIgnoreCase) ||
            blockCode.Contains("forge", StringComparison.OrdinalIgnoreCase) ||
            blockCode.Contains("firepit", StringComparison.OrdinalIgnoreCase))
        {
            return new BaseIndicator
            {
                Position = position,
                Type = BaseIndicatorType.Crafting,
                Priority = 5,
                Range = 10f
            };
        }

        // Constructed blocks (lower priority - walls/structures)
        if (blockCode.Contains("bricks", StringComparison.OrdinalIgnoreCase) ||
            blockCode.Contains("planks", StringComparison.OrdinalIgnoreCase) ||
            (blockCode.Contains("stone", StringComparison.OrdinalIgnoreCase) && !blockCode.Contains("cobble", StringComparison.OrdinalIgnoreCase)))
        {
            return new BaseIndicator
            {
                Position = position,
                Type = BaseIndicatorType.Construction,
                Priority = 2,
                Range = 8f
            };
        }

        return null;
    }

    /// <summary>
    /// Analyze all indicators to determine base location and type
    /// </summary>
    private DetectedBase? AnalyzeIndicators(List<BaseIndicator> indicators, Vec3d playerPos)
    {
        if (indicators.Count < 3) // Need at least 3 indicators for a valid base
            return null;

        // Group indicators by proximity
        var clusters = FindClusters(indicators);
        var bestCluster = clusters.OrderByDescending(c => c.Sum(i => i.Priority)).FirstOrDefault();

        if (bestCluster == null || bestCluster.Count < 2)
            return null;

        // Calculate center of base - prioritize doors and beds for better targeting
        Vec3d center;
        var priorityIndicators = bestCluster.Where(i => i.Type == BaseIndicatorType.Door || i.Type == BaseIndicatorType.Bed).ToList();
        if (priorityIndicators.Count > 0)
        {
            // Use doors and beds as primary targeting points
            var centerX = priorityIndicators.Average(i => i.Position.X);
            var centerY = priorityIndicators.Average(i => i.Position.Y);
            var centerZ = priorityIndicators.Average(i => i.Position.Z);
            center = new Vec3d(centerX, centerY, centerZ);

            DebugLogger.Event("Base center calculated from priority indicators",
                $"Priority indicators: {priorityIndicators.Count}, Center: {centerX:F1},{centerY:F1},{centerZ:F1}");
        }
        else
        {
            // Fallback to all indicators
            var centerX = bestCluster.Average(i => i.Position.X);
            var centerY = bestCluster.Average(i => i.Position.Y);
            var centerZ = bestCluster.Average(i => i.Position.Z);
            center = new Vec3d(centerX, centerY, centerZ);

            DebugLogger.Event("Base center calculated from all indicators",
                $"All indicators: {bestCluster.Count}, Center: {centerX:F1},{centerY:F1},{centerZ:F1}");
        }

        // Calculate base score and determine type
        var baseScore = bestCluster.Sum(i => i.Priority);
        var baseType = DetermineBaseType(bestCluster);

        // Find bed position if exists
        Vec3d? bedPosition = bestCluster
            .Where(i => i.Type == BaseIndicatorType.Bed)
            .Select(i => i.Position)
            .FirstOrDefault();

        return new DetectedBase
        {
            Center = center,
            BaseScore = baseScore,
            BaseType = baseType,
            BedPosition = bedPosition,
            DetectedAt = DateTime.UtcNow,
            IndicatorCount = bestCluster.Count,
            DistanceFromPlayer = (float)center.DistanceTo(playerPos)
        };
    }

    /// <summary>
    /// Find clusters of indicators that are close to each other
    /// </summary>
    private List<List<BaseIndicator>> FindClusters(List<BaseIndicator> indicators)
    {
        var clusters = new List<List<BaseIndicator>>();
        var processed = new HashSet<BaseIndicator>();

        foreach (var indicator in indicators)
        {
            if (processed.Contains(indicator))
                continue;

            var cluster = new List<BaseIndicator> { indicator };
            processed.Add(indicator);

            // Find nearby indicators
            foreach (var other in indicators)
            {
                if (processed.Contains(other))
                    continue;

                var distance = indicator.Position.DistanceTo(other.Position);
                if (distance <= Math.Max(indicator.Range, other.Range))
                {
                    cluster.Add(other);
                    processed.Add(other);
                }
            }

            if (cluster.Count >= 2) // Only keep clusters with at least 2 indicators
            {
                clusters.Add(cluster);
            }
        }

        return clusters;
    }

    /// <summary>
    /// Determine base type from indicators
    /// </summary>
    private string DetermineBaseType(List<BaseIndicator> indicators)
    {
        var hasBed = indicators.Any(i => i.Type == BaseIndicatorType.Bed);
        var hasDoor = indicators.Any(i => i.Type == BaseIndicatorType.Door);
        var hasStorage = indicators.Any(i => i.Type == BaseIndicatorType.Storage);
        var hasCrafting = indicators.Any(i => i.Type == BaseIndicatorType.Crafting);

        if (hasBed && hasDoor && hasStorage && hasCrafting)
            return "CompleteBase";
        if (hasBed && hasDoor)
            return "HomeBase";
        if (hasStorage && hasCrafting)
            return "WorkshopBase";
        if (hasDoor || hasBed)
            return "BasicBase";

        return "Outpost";
    }

    /// <summary>
    /// Get cached base for player
    /// </summary>
    public DetectedBase? GetPlayerBase(string playerUID)
    {
        return playerBases.TryGetValue(playerUID, out var detectedBase) ? detectedBase : null;
    }
}

/// <summary>
/// Represents a detected player base
/// </summary>
public class DetectedBase
{
    public Vec3d Center { get; set; } = new Vec3d();
    public string BaseType { get; set; } = "";
    public int BaseScore { get; set; }
    public Vec3d? BedPosition { get; set; }
    public DateTime DetectedAt { get; set; }
    public string PlayerUID { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int IndicatorCount { get; set; }
    public float DistanceFromPlayer { get; set; }
}

/// <summary>
/// An indicator that suggests the presence of a player base
/// </summary>
public class BaseIndicator
{
    public Vec3d Position { get; set; } = new Vec3d();
    public BaseIndicatorType Type { get; set; }
    public int Priority { get; set; }
    public float Range { get; set; }
}

/// <summary>
/// Types of base indicators
/// </summary>
public enum BaseIndicatorType
{
    Door,
    Bed,
    Storage,
    Crafting,
    Construction
}
