using System;
using System.Collections.Generic;
using System.Linq;
using HueHordes.Models;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Detects and analyzes player bases in the world
/// </summary>
public class BaseDetection
{
    private readonly ICoreServerAPI sapi;
    private readonly Dictionary<string, PlayerBase> detectedBases = new();

    public BaseDetection(ICoreServerAPI serverApi)
    {
        sapi = serverApi;
    }

    /// <summary>
    /// Detect or update a player base around their current position
    /// </summary>
    public PlayerBase? DetectPlayerBase(IServerPlayer player, int scanRadius = 50)
    {
        var playerPos = player.Entity.ServerPos.XYZ;
        var playerUID = player.PlayerUID;

        // Check if we already have a base for this player
        if (detectedBases.TryGetValue(playerUID, out var existingBase))
        {
            // Update if player has moved significantly from base center
            if (playerPos.SquareDistanceTo(existingBase.Center) < 100 * 100) // Within 100 blocks
            {
                existingBase.LastDetectedDay = sapi.World.Calendar.TotalDays;
                return existingBase;
            }
        }

        // Perform new base detection
        var newBase = ScanForPlayerBase(player, scanRadius);
        if (newBase != null)
        {
            detectedBases[playerUID] = newBase;
        }

        return newBase;
    }

    /// <summary>
    /// Get cached player base
    /// </summary>
    public PlayerBase? GetPlayerBase(string playerUID)
    {
        detectedBases.TryGetValue(playerUID, out var playerBase);
        return playerBase;
    }

    /// <summary>
    /// Scan area around player to detect base structure
    /// </summary>
    private PlayerBase? ScanForPlayerBase(IServerPlayer player, int radius)
    {
        var playerPos = player.Entity.ServerPos.XYZ;
        var blockAccessor = sapi.World.BlockAccessor;

        // Look for bed first as base center
        var bedPosition = FindNearbyBed(playerPos, radius);
        var baseCenter = bedPosition ?? playerPos;

        // Scan for artificial structures
        var artificialBlocks = ScanForArtificialBlocks(baseCenter, radius);
        if (artificialBlocks.Count < 10) // Need at least some built blocks
        {
            // Create simple base around player/bed position
            return CreateSimpleBase(player, baseCenter, bedPosition);
        }

        // Analyze structure to determine base boundaries
        var boundingBox = CalculateBoundingBox(artificialBlocks);
        var wallBlocks = IdentifyWallBlocks(artificialBlocks, boundingBox);
        var entrances = DetectEntrances(wallBlocks, boundingBox);
        var interiorArea = CalculateInteriorArea(boundingBox, wallBlocks, entrances);

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
    /// Find nearby bed that could serve as base center
    /// </summary>
    private Vec3d? FindNearbyBed(Vec3d centerPos, int radius)
    {
        var blockAccessor = sapi.World.BlockAccessor;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -10; y <= 10; y++) // Limit vertical search
            {
                for (int z = -radius; z <= radius; z++)
                {
                    var pos = new BlockPos((int)(centerPos.X + x), (int)(centerPos.Y + y), (int)(centerPos.Z + z));
                    var block = blockAccessor.GetBlock(pos);

                    if (block?.Code?.Path?.Contains("bed") == true)
                    {
                        return new Vec3d(pos.X, pos.Y, pos.Z);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Scan for blocks that appear to be player-built
    /// </summary>
    private List<Vec3d> ScanForArtificialBlocks(Vec3d center, int radius)
    {
        var artificialBlocks = new List<Vec3d>();
        var blockAccessor = sapi.World.BlockAccessor;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -10; y <= 10; y++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    var pos = new BlockPos((int)(center.X + x), (int)(center.Y + y), (int)(center.Z + z));
                    var block = blockAccessor.GetBlock(pos);

                    if (IsArtificialBlock(block))
                    {
                        artificialBlocks.Add(new Vec3d(pos.X, pos.Y, pos.Z));
                    }
                }
            }
        }

        return artificialBlocks;
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
    /// Identify blocks that form walls/perimeter
    /// </summary>
    private List<Vec3d> IdentifyWallBlocks(List<Vec3d> artificialBlocks, Cuboidf boundingBox)
    {
        var wallBlocks = new List<Vec3d>();

        foreach (var block in artificialBlocks)
        {
            // Consider blocks near the perimeter as wall blocks
            bool isNearEdge =
                Math.Abs(block.X - boundingBox.MinX) < 2 ||
                Math.Abs(block.X - boundingBox.MaxX) < 2 ||
                Math.Abs(block.Z - boundingBox.MinZ) < 2 ||
                Math.Abs(block.Z - boundingBox.MaxZ) < 2;

            if (isNearEdge)
            {
                wallBlocks.Add(block);
            }
        }

        return wallBlocks;
    }

    /// <summary>
    /// Detect entrance points in the base
    /// </summary>
    private List<Vec3d> DetectEntrances(List<Vec3d> wallBlocks, Cuboidf boundingBox)
    {
        var entrances = new List<Vec3d>();
        var blockAccessor = sapi.World.BlockAccessor;

        // Look for gaps in wall blocks that could be entrances
        // This is a simplified approach - scan perimeter for openings
        int perimeter = (int)((boundingBox.MaxX - boundingBox.MinX) + (boundingBox.MaxZ - boundingBox.MinZ)) * 2;

        for (int i = 0; i < perimeter; i++)
        {
            // Sample points around perimeter
            double angle = (2 * Math.PI * i) / perimeter;
            double centerX = (boundingBox.MinX + boundingBox.MaxX) / 2;
            double centerZ = (boundingBox.MinZ + boundingBox.MaxZ) / 2;
            double radiusX = (boundingBox.MaxX - boundingBox.MinX) / 2;
            double radiusZ = (boundingBox.MaxZ - boundingBox.MinZ) / 2;

            double x = centerX + Math.Cos(angle) * radiusX;
            double z = centerZ + Math.Sin(angle) * radiusZ;

            var pos = new BlockPos((int)x, (int)boundingBox.MinY, (int)z);
            var block = blockAccessor.GetBlock(pos);

            // If there's an opening (air, door), consider it an entrance
            if (block.Code.Path == "air" || block.Code.Path.Contains("door"))
            {
                entrances.Add(new Vec3d(pos.X, pos.Y, pos.Z));
            }
        }

        // If no entrances detected, add one at the edge closest to world spawn
        if (entrances.Count == 0)
        {
            var centerPos = new Vec3d((boundingBox.MinX + boundingBox.MaxX) / 2, boundingBox.MinY, (boundingBox.MinZ + boundingBox.MaxZ) / 2);
            var radiusX = (boundingBox.MaxX - boundingBox.MinX) / 2;
            var radiusZ = (boundingBox.MaxZ - boundingBox.MinZ) / 2;

            // Add entrance point at the edge closest to center
            var entrance = centerPos + new Vec3d(radiusX, 0, 0); // Simple fallback
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

        // Simple approach: add points within bounding box that aren't wall blocks
        for (int x = (int)boundingBox.MinX; x < boundingBox.MaxX; x += 2)
        {
            for (int z = (int)boundingBox.MinZ; z < boundingBox.MaxZ; z += 2)
            {
                var point = new Vec3d(x, boundingBox.MinY, z);

                // Skip if too close to wall blocks
                bool tooCloseToWall = wallBlocks.Any(wall => point.SquareDistanceTo(wall) < 4);
                if (!tooCloseToWall)
                {
                    interiorArea.Add(point);
                }
            }
        }

        return interiorArea;
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
            Entrances = new List<Vec3d> { center }, // Player position as entrance
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

        // More artificial blocks = higher confidence
        confidence += Math.Min(0.4f, artificialBlocks.Count / 100.0f);

        // Clear walls increase confidence
        if (wallBlocks.Count > 10)
            confidence += 0.3f;

        // Detected entrances increase confidence
        confidence += entrances.Count * 0.1f;

        return Math.Min(1.0f, confidence);
    }
}