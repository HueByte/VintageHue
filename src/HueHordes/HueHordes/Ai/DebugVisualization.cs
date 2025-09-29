using System;
using System.Collections.Generic;
using System.Linq;
using HueHordes.Debug;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Debug visualization system for base detection and pathfinding
/// Shows colored particles above detected structures and along mob paths
/// </summary>
public class DebugVisualization
{
    private readonly ICoreServerAPI sapi;
    private readonly HashSet<Vec3d> activeBasePositions = new();
    private readonly Dictionary<string, List<Vec3d>> entityPathPositions = new();
    private bool pathVisualizationEnabled = false;

    // Particle properties for different base indicator types
    private readonly Dictionary<BaseIndicatorType, SimpleParticleProperties> indicatorParticles = new();
    private SimpleParticleProperties pathParticles = null!;

    private void InitializeParticles()
    {
        try
        {
            // Create particle properties for each base indicator type
            indicatorParticles[BaseIndicatorType.Door] = CreateBaseParticle(ColorUtil.ColorFromRgba(255, 0, 0, 255));        // Red
            indicatorParticles[BaseIndicatorType.Bed] = CreateBaseParticle(ColorUtil.ColorFromRgba(255, 165, 0, 255));      // Orange
            indicatorParticles[BaseIndicatorType.Storage] = CreateBaseParticle(ColorUtil.ColorFromRgba(0, 0, 255, 255));    // Blue
            indicatorParticles[BaseIndicatorType.Crafting] = CreateBaseParticle(ColorUtil.ColorFromRgba(128, 0, 128, 255)); // Purple
            indicatorParticles[BaseIndicatorType.Construction] = CreateBaseParticle(ColorUtil.ColorFromRgba(255, 255, 255, 255)); // White

            // Create path particles (green)
            pathParticles = CreatePathParticle(ColorUtil.ColorFromRgba(0, 255, 0, 255));

            DebugLogger.AIEvent("Particles initialized successfully", "All particle types created", "DebugVisualization");
        }
        catch (Exception ex)
        {
            DebugLogger.Error("Failed to initialize particles", ex);
        }
    }

    /// <summary>
    /// Create particle properties for base indicators
    /// </summary>
    private SimpleParticleProperties CreateBaseParticle(int color)
    {
        var particles = new SimpleParticleProperties(
            1, 1,                    // MinQuantity, AddQuantity
            color,                   // Color
            new Vec3d(),             // MinPos (will be set when spawning)
            new Vec3d(),             // AddPos (no random offset)
            new Vec3f(),             // MinVelocity (stationary)
            new Vec3f()              // AddVelocity (no random velocity)
        );

        // Configure particle appearance and behavior
        particles.LifeLength = 10f;                    // 10 seconds lifetime
        particles.GravityEffect = 0f;                  // Float in air
        particles.MinSize = 3.0f;                      // Particle size (10x bigger)
        particles.MaxSize = 5.0f;                      // Max particle size (10x bigger)
        particles.ParticleModel = EnumParticleModel.Cube; // Cube particles
        particles.SelfPropelled = true;                // Keep stationary

        return particles;
    }

    /// <summary>
    /// Create particle properties for path visualization
    /// </summary>
    private SimpleParticleProperties CreatePathParticle(int color)
    {
        var particles = new SimpleParticleProperties(
            1, 1,                    // MinQuantity, AddQuantity (both set to 1 for consistent spawning)
            color,                   // Color
            new Vec3d(),             // MinPos (will be set when spawning)
            new Vec3d(0.2, 0.2, 0.2), // AddPos (slightly larger random offset for visibility)
            new Vec3f(),             // MinVelocity (stationary)
            new Vec3f()              // AddVelocity (no random velocity)
        );

        // Configure path particle appearance
        particles.LifeLength = 5f;                     // 5 seconds lifetime
        particles.GravityEffect = 0f;                  // Float in air
        particles.MinSize = 2.0f;                      // Particles for paths (10x bigger)
        particles.MaxSize = 3.0f;                      // (10x bigger)
        particles.ParticleModel = EnumParticleModel.Cube; // Cube particles
        particles.SelfPropelled = true;                // Keep stationary

        return particles;
    }

    public DebugVisualization(ICoreServerAPI serverApi)
    {
        sapi = serverApi ?? throw new ArgumentNullException(nameof(serverApi));
        InitializeParticles();
    }

    /// <summary>
    /// Show a particle above a base indicator block
    /// </summary>
    public void ShowBaseIndicator(BaseIndicator indicator)
    {
        // Check if we already have a particle at this position
        if (activeBasePositions.Contains(indicator.Position))
        {
            DebugLogger.AIEvent("Duplicate base indicator ignored",
                $"Position: {indicator.Position.X:F1},{indicator.Position.Y:F1},{indicator.Position.Z:F1}",
                "DebugVisualization");
            return;
        }

        // Check if there's open air above the block
        var particlePos = GetParticlePosition(indicator.Position);
        if (particlePos == null)
        {
            DebugLogger.AIEvent("No air space found for particle",
                $"Position: {indicator.Position.X:F1},{indicator.Position.Y:F1},{indicator.Position.Z:F1}",
                "DebugVisualization");
            return;
        }

        // Get the appropriate particle properties for this indicator type
        if (!indicatorParticles.TryGetValue(indicator.Type, out var particles))
        {
            DebugLogger.AIEvent("Unknown indicator type", $"Type: {indicator.Type}", "DebugVisualization");
            return;
        }

        // Set particle position and spawn
        particles.MinPos = particlePos;
        sapi.World.SpawnParticles(particles);

        // Track this position to avoid duplicate particles
        activeBasePositions.Add(indicator.Position);

        DebugLogger.AIEvent("Base particle spawned",
            $"Type: {indicator.Type}, Position: {particlePos.X:F1},{particlePos.Y:F1},{particlePos.Z:F1}",
            "DebugVisualization");
    }

    /// <summary>
    /// Show particles along an entity's path at entity base level coordinates
    /// Path coordinates represent Y=2 level (entity base/bottom)
    /// </summary>
    public void ShowEntityPath(string entityId, List<Vec3d> pathNodes)
    {
        if (!pathVisualizationEnabled)
        {
            DebugLogger.AIEvent("Path visualization disabled",
                $"Entity: {entityId}, Nodes: {pathNodes?.Count ?? 0}",
                "DebugVisualization");
            return;
        }

        if (pathNodes == null || pathNodes.Count == 0)
        {
            DebugLogger.AIEvent("No path nodes provided",
                $"Entity: {entityId}",
                "DebugVisualization");
            return;
        }

        // Clear previous path positions for this entity
        ClearEntityPathParticles(entityId);

        var pathPositions = new List<Vec3d>();
        int particlesSpawned = 0;

        // Spawn particles along the path
        var coordLog = new List<string>();
        foreach (var node in pathNodes)
        {
            var particlePos = new Vec3d(node.X + 0.5, node.Y, node.Z + 0.5); // Entity base level (Y=2 coordinates)
            var coordStr = $"({node.X:F0},{node.Y:F0},{node.Z:F0})";
            coordLog.Add(coordStr);

            try
            {
                // Set particle position and spawn
                pathParticles.MinPos = particlePos;
                sapi.World.SpawnParticles(pathParticles);
                particlesSpawned++;

                // Log each particle placement
                DebugLogger.AIPath("Particles", "ParticleSpawned",
                    $"Particle #{particlesSpawned} at entity base {coordStr} -> world pos ({particlePos.X:F1},{particlePos.Y:F1},{particlePos.Z:F1})",
                    $"Entity {entityId}");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to spawn path particle at {particlePos.X:F1},{particlePos.Y:F1},{particlePos.Z:F1}", ex);
            }

            pathPositions.Add(node);
        }

        if (pathPositions.Count > 0)
        {
            entityPathPositions[entityId] = pathPositions;

            // Log summary with all coordinates
            var coordinatesList = string.Join(" -> ", coordLog);
            DebugLogger.AIPath("Particles", "PathComplete",
                $"Complete path coordinates: {coordinatesList}",
                $"Entity {entityId}: {pathPositions.Count} nodes, {particlesSpawned} particles");

            DebugLogger.AIEvent("Path particles spawned",
                $"Entity: {entityId}, Total nodes: {pathPositions.Count}, Particles spawned: {particlesSpawned}",
                "DebugVisualization");
        }
    }

    /// <summary>
    /// Enable or disable path visualization
    /// </summary>
    public void EnablePathVisualization(bool enabled)
    {
        var wasEnabled = pathVisualizationEnabled;
        pathVisualizationEnabled = enabled;

        if (!enabled && wasEnabled)
        {
            // Clear all path particles when disabling
            ClearAllPathParticles();
        }

        DebugLogger.AIEvent("Path visualization toggled",
            $"Was: {wasEnabled}, Now: {enabled}",
            "DebugVisualization");
    }

    /// <summary>
    /// Enable path visualization by default (call this to start seeing path particles)
    /// </summary>
    public void EnablePathVisualizationByDefault()
    {
        EnablePathVisualization(true);
        DebugLogger.AIEvent("Path visualization enabled by default",
            "Path particles will now be visible",
            "DebugVisualization");
    }

    /// <summary>
    /// Clear all base indicator particles
    /// </summary>
    public void ClearBaseParticles()
    {
        var clearedCount = activeBasePositions.Count;
        activeBasePositions.Clear();

        DebugLogger.AIEvent("Base particles cleared",
            $"Cleared {clearedCount} particle positions",
            "DebugVisualization");
    }

    /// <summary>
    /// Clear path particles for a specific entity
    /// </summary>
    public void ClearEntityPathParticles(string entityId)
    {
        if (entityPathPositions.TryGetValue(entityId, out var positions))
        {
            entityPathPositions.Remove(entityId);
            DebugLogger.AIEvent("Entity path particles cleared",
                $"Entity: {entityId}, Positions: {positions.Count}",
                "DebugVisualization");
        }
    }

    /// <summary>
    /// Clear all path particles
    /// </summary>
    public void ClearAllPathParticles()
    {
        var clearedEntities = entityPathPositions.Count;
        entityPathPositions.Clear();

        DebugLogger.AIEvent("All path particles cleared",
            $"Cleared paths for {clearedEntities} entities",
            "DebugVisualization");
    }

    /// <summary>
    /// Clear all particles (base and path)
    /// </summary>
    public void ClearAllParticles()
    {
        ClearBaseParticles();
        ClearAllPathParticles();
    }

    /// <summary>
    /// Get particle position above a block (only if there's open air)
    /// </summary>
    private Vec3d? GetParticlePosition(Vec3d blockPosition)
    {
        // Always try to place particles at entity level (1 block above ground)
        var entityHeight = 1;
        var entityPos = new BlockPos((int)blockPosition.X, (int)blockPosition.Y + entityHeight, (int)blockPosition.Z);

        if (sapi.World.BlockAccessor.IsValidPos(entityPos))
        {
            var entityBlock = sapi.World.BlockAccessor.GetBlock(entityPos);
            if (entityBlock.Id == 0) // Air block at entity level
            {
                return new Vec3d(blockPosition.X + 0.5, blockPosition.Y + entityHeight + 0.5, blockPosition.Z + 0.5);
            }
        }

        // Fallback: Check positions above the block for open air
        for (int y = 1; y <= 3; y++) // Limited range to stay near entity level
        {
            if (y == entityHeight) continue; // Already checked

            var checkPos = new BlockPos((int)blockPosition.X, (int)blockPosition.Y + y, (int)blockPosition.Z);

            if (!sapi.World.BlockAccessor.IsValidPos(checkPos))
                continue;

            var block = sapi.World.BlockAccessor.GetBlock(checkPos);
            if (block.Id == 0) // Air block
            {
                return new Vec3d(blockPosition.X + 0.5, blockPosition.Y + y + 0.5, blockPosition.Z + 0.5);
            }
        }

        return null; // No open air above this block
    }

    /// <summary>
    /// Refresh all active particles (call periodically to maintain visibility)
    /// </summary>
    public void RefreshParticles()
    {
        // Refresh base indicator particles
        foreach (var position in activeBasePositions)
        {
            var particlePos = GetParticlePosition(position);
            if (particlePos != null)
            {
                // Find the appropriate particle type for this position
                // Since we don't store the type with position, we'll use a default particle
                // In a real implementation, you might want to store BaseIndicator objects instead
                var defaultParticle = indicatorParticles.Values.FirstOrDefault();
                if (defaultParticle != null)
                {
                    defaultParticle.MinPos = particlePos;
                    sapi.World.SpawnParticles(defaultParticle);
                }
            }
        }

        // Refresh path particles if enabled
        if (pathVisualizationEnabled)
        {
            foreach (var entityPath in entityPathPositions)
            {
                foreach (var node in entityPath.Value)
                {
                    var particlePos = new Vec3d(node.X + 0.5, node.Y + 1, node.Z + 0.5);
                    pathParticles.MinPos = particlePos;
                    sapi.World.SpawnParticles(pathParticles);
                }
            }
        }
    }

    /// <summary>
    /// Get debug visualization status
    /// </summary>
    public bool IsPathVisualizationEnabled => pathVisualizationEnabled;

    /// <summary>
    /// Get count of active base positions being visualized
    /// </summary>
    public int ActiveBaseCount => activeBasePositions.Count;

    /// <summary>
    /// Get count of entities with active path visualization
    /// </summary>
    public int ActivePathCount => entityPathPositions.Count;
}
