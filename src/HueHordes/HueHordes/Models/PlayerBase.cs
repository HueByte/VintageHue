using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace HueHordes.Models;

/// <summary>
/// Represents a detected player base with its boundaries, entrances, and center
/// </summary>
public class PlayerBase
{
    /// <summary>
    /// The player this base belongs to
    /// </summary>
    public string PlayerUID { get; set; } = string.Empty;

    /// <summary>
    /// Center point of the base (typically the bed location)
    /// </summary>
    public Vec3d Center { get; set; } = Vec3d.Zero;

    /// <summary>
    /// Bounding box that encompasses the entire base
    /// </summary>
    public Cuboidf BoundingBox { get; set; } = new();

    /// <summary>
    /// Detected entrance/gate positions
    /// </summary>
    public List<Vec3d> Entrances { get; set; } = new();

    /// <summary>
    /// Wall segments that form the base perimeter
    /// </summary>
    public List<Vec3d> WallBlocks { get; set; } = new();

    /// <summary>
    /// Interior area blocks (for patrol purposes)
    /// </summary>
    public List<Vec3d> InteriorArea { get; set; } = new();

    /// <summary>
    /// When this base was last detected/updated
    /// </summary>
    public double LastDetectedDay { get; set; }

    /// <summary>
    /// Confidence score of base detection (0.0 - 1.0)
    /// </summary>
    public float DetectionConfidence { get; set; }

    /// <summary>
    /// Base type classification
    /// </summary>
    public BaseType Type { get; set; } = BaseType.Simple;

    /// <summary>
    /// Whether this base has a clear perimeter
    /// </summary>
    public bool HasEnclosure { get; set; }

    /// <summary>
    /// The bed position that defines this base center
    /// </summary>
    public Vec3d? BedPosition { get; set; }

    /// <summary>
    /// Get a random patrol point within the base interior
    /// </summary>
    public Vec3d GetRandomPatrolPoint(System.Random rand)
    {
        if (InteriorArea.Count == 0)
        {
            // Fallback to area around center
            double offsetX = (rand.NextDouble() - 0.5) * 10; // 10 block radius
            double offsetZ = (rand.NextDouble() - 0.5) * 10;
            return new Vec3d(Center.X + offsetX, Center.Y, Center.Z + offsetZ);
        }

        return InteriorArea[rand.Next(InteriorArea.Count)];
    }

    /// <summary>
    /// Get the best entrance for approaching from a given position
    /// </summary>
    public Vec3d GetNearestEntrance(Vec3d fromPosition)
    {
        if (Entrances.Count == 0)
            return Center;

        Vec3d nearest = Entrances[0];
        double shortestDist = fromPosition.SquareDistanceTo(nearest);

        foreach (var entrance in Entrances)
        {
            double dist = fromPosition.SquareDistanceTo(entrance);
            if (dist < shortestDist)
            {
                shortestDist = dist;
                nearest = entrance;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Check if a position is within the base boundaries
    /// </summary>
    public bool ContainsPosition(Vec3d position)
    {
        return BoundingBox.Contains((float)position.X, (float)position.Y, (float)position.Z);
    }

    /// <summary>
    /// Get safe spawn positions outside the base
    /// </summary>
    public List<Vec3d> GetSafeSpawnPositions(int count, float minDistance = 20f, float maxDistance = 40f)
    {
        var spawnPoints = new List<Vec3d>();
        var rand = new System.Random();

        for (int i = 0; i < count * 3 && spawnPoints.Count < count; i++) // Try up to 3x attempts
        {
            double angle = rand.NextDouble() * System.Math.PI * 2;
            double distance = minDistance + (rand.NextDouble() * (maxDistance - minDistance));

            double x = Center.X + System.Math.Cos(angle) * distance;
            double z = Center.Z + System.Math.Sin(angle) * distance;
            double y = Center.Y; // Will be adjusted by terrain height later

            var candidate = new Vec3d(x, y, z);

            // Make sure it's not too close to existing spawn points
            bool tooClose = false;
            foreach (var existing in spawnPoints)
            {
                if (candidate.SquareDistanceTo(existing) < (minDistance / 2) * (minDistance / 2))
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                spawnPoints.Add(candidate);
            }
        }

        return spawnPoints;
    }
}

/// <summary>
/// Classification of base types
/// </summary>
public enum BaseType
{
    /// <summary>
    /// Simple structure without clear walls
    /// </summary>
    Simple,

    /// <summary>
    /// Walled base with clear perimeter
    /// </summary>
    Walled,

    /// <summary>
    /// Complex multi-level structure
    /// </summary>
    Complex,

    /// <summary>
    /// Underground/cave base
    /// </summary>
    Underground
}