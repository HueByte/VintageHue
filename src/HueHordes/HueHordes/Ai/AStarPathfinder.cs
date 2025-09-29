using System;
using System.Collections.Generic;
using System.Linq;
using HueHordes.Debug;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// 3D integer vector for pathfinding
/// </summary>
public struct Vec3i : IEquatable<Vec3i>
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    public Vec3i(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public bool Equals(Vec3i other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    public override bool Equals(object? obj)
    {
        return obj is Vec3i other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    public static bool operator ==(Vec3i left, Vec3i right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Vec3i left, Vec3i right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }
}

/// <summary>
/// A* pathfinding implementation for 3D space with jump capability
/// Handles terrain navigation with 1-block jump height
/// </summary>
public class AStarPathfinder
{
    private readonly ICoreServerAPI sapi;
    private readonly Dictionary<Vec3i, AStarNode> openSet = new();
    private readonly HashSet<Vec3i> closedSet = new();
    private readonly PriorityQueue<AStarNode> openQueue = new();
    private readonly DebugVisualization? debugVisualization;

    // Movement costs
    private const float MOVE_COST_HORIZONTAL = 1.0f;
    private const float MOVE_COST_VERTICAL = 1.4f; // Slightly higher cost for vertical movement
    private const float MOVE_COST_DIAGONAL = 1.4f;
    private const float JUMP_COST_PENALTY = 2.0f; // Additional cost for jumping

    // Movement constraints
    private const int MAX_JUMP_HEIGHT = 1;
    private const int MAX_FALL_HEIGHT = 10;

    public AStarPathfinder(ICoreServerAPI serverApi, bool enablePathVisualization = true)
    {
        sapi = serverApi ?? throw new ArgumentNullException(nameof(serverApi));

        // Initialize debug visualization if enabled
        if (enablePathVisualization)
        {
            debugVisualization = new DebugVisualization(serverApi);
            debugVisualization.EnablePathVisualizationByDefault();
        }
    }

    /// <summary>
    /// Find optimal path using A* algorithm in 3D space
    /// </summary>
    public List<Vec3d> FindPath(Vec3d start, Vec3d end, int maxNodes = 1000)
    {
        // Generate a unique entity ID for this path request
        var entityId = $"path_{start.X:F0}_{start.Y:F0}_{start.Z:F0}_to_{end.X:F0}_{end.Y:F0}_{end.Z:F0}_{DateTime.UtcNow.Ticks}";
        return FindPath(start, end, entityId, maxNodes);
    }

    /// <summary>
    /// Find optimal path using A* algorithm in 3D space with specific entity ID for visualization
    /// Uses entity base level (Y=2) as working coordinates
    /// </summary>
    public List<Vec3d> FindPath(Vec3d start, Vec3d end, string entityId, int maxNodes = 1000)
    {
        // Convert entity positions to entity base coordinates (Y=2 level)
        // Entity at Y=3.4 -> entity base Y=2, entity at Y=5.0 -> entity base Y=4
        var startNode = new Vec3i((int)start.X, (int)Math.Floor(start.Y), (int)start.Z);
        var endNode = new Vec3i((int)end.X, (int)Math.Floor(end.Y), (int)end.Z);

        // Check if the target is inside a wall/unreachable, find nearby accessible position
        if (!IsPositionWalkable(endNode))
        {
            var accessibleTarget = FindNearestAccessiblePosition(endNode);
            if (accessibleTarget.HasValue)
            {
                endNode = accessibleTarget.Value;
                end = new Vec3d(endNode.X + 0.5, endNode.Y, endNode.Z + 0.5);

                DebugLogger.AIPath("AStar", "TargetAdjusted",
                    $"Original target was blocked, using {endNode.X},{endNode.Y},{endNode.Z}",
                    "Target inside wall - finding accessible alternative");
            }
        }

        // Clear previous search data
        openSet.Clear();
        closedSet.Clear();
        openQueue.Clear();

        // Initialize start node
        var startAStarNode = new AStarNode
        {
            Position = startNode,
            GCost = 0,
            HCost = CalculateHeuristic(startNode, endNode),
            Parent = null
        };

        openSet[startNode] = startAStarNode;
        openQueue.Enqueue(startAStarNode);

        var nodesSearched = 0;
        var pathFound = false;

        while (openQueue.Count > 0 && nodesSearched < maxNodes)
        {
            var currentNode = openQueue.Dequeue();
            var currentPos = currentNode.Position;

            // Remove from open set and add to closed set
            openSet.Remove(currentPos);
            closedSet.Add(currentPos);

            // Check if we've reached the destination
            if (IsAtDestination(currentPos, endNode))
            {
                pathFound = true;
                var path = ReconstructPath(currentNode);

                DebugLogger.AIPath("AStar", "PathFound",
                    $"{path.Count} waypoints, {nodesSearched} nodes searched",
                    $"From {start.X:F1},{start.Y:F1},{start.Z:F1} to {end.X:F1},{end.Y:F1},{end.Z:F1}");

                // Show path particles for successful path
                ShowPathParticles(path, entityId);

                return path;
            }

            // Explore neighbors in 3D space
            foreach (var neighbor in GetValidNeighbors(currentPos))
            {
                if (closedSet.Contains(neighbor))
                    continue;

                if (!IsPositionWalkable(neighbor))
                    continue;

                var moveCost = CalculateMoveCost(currentPos, neighbor);
                var newGCost = currentNode.GCost + moveCost;

                // Check if this path to neighbor is better than any previous one
                if (openSet.TryGetValue(neighbor, out var existingNode))
                {
                    if (newGCost < existingNode.GCost)
                    {
                        // Update existing node with better path
                        existingNode.GCost = newGCost;
                        existingNode.Parent = currentNode;
                    }
                }
                else
                {
                    // Add new node to open set
                    var neighborNode = new AStarNode
                    {
                        Position = neighbor,
                        GCost = newGCost,
                        HCost = CalculateHeuristic(neighbor, endNode),
                        Parent = currentNode
                    };

                    openSet[neighbor] = neighborNode;
                    openQueue.Enqueue(neighborNode);
                }
            }

            nodesSearched++;
        }

        if (!pathFound)
        {
            DebugLogger.AIPath("AStar", "NoPathFound",
                $"{nodesSearched} nodes searched, max distance reached",
                $"From {start.X:F1},{start.Y:F1},{start.Z:F1} to {end.X:F1},{end.Y:F1},{end.Z:F1}");

            // Return path to closest reachable position
            return GetPathToClosestReachable(start, end, entityId);
        }

        return new List<Vec3d>();
    }

    /// <summary>
    /// Get all valid neighbor positions in 3D space
    /// </summary>
    private IEnumerable<Vec3i> GetNeighbors(Vec3i position)
    {
        // 26 possible directions in 3D space (3x3x3 cube minus center)
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dy == 0 && dz == 0)
                        continue;

                    var neighbor = new Vec3i(
                        position.X + dx,
                        position.Y + dy,
                        position.Z + dz
                    );

                    // Check movement constraints
                    if (IsValidMovement(position, neighbor))
                    {
                        yield return neighbor;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Check if movement from one position to another is valid
    /// Positions represent entity base level coordinates
    /// Movement limited to ±1 block vertically for symmetry
    /// </summary>
    private bool IsValidMovement(Vec3i from, Vec3i to)
    {
        var deltaY = to.Y - from.Y;

        // Entity can jump up 1 block (base level Y to Y+1)
        if (deltaY > 1)
            return false;

        // Entity can walk down 1 block (base level Y to Y-1)
        // Symmetrical with upward movement, no jumping required
        if (deltaY < -1)
            return false;

        // For diagonal movement, check corners to prevent cutting through blocks
        if (Math.Abs(to.X - from.X) > 0 && Math.Abs(to.Z - from.Z) > 0)
        {
            return IsValidDiagonalMovement(from, to);
        }

        return true;
    }

    /// <summary>
    /// Check if a position is walkable (has air space and solid ground)
    /// Position Y represents the entity base level (Y=2 coordinates)
    /// Y-1=ground block, Y=entity base, Y+1=entity body
    /// </summary>
    private bool IsPositionWalkable(Vec3i position)
    {
        var blockPos = new BlockPos(position.X, position.Y, position.Z);

        // Check if position is valid in world
        if (!sapi.World.BlockAccessor.IsValidPos(blockPos))
            return false;

        // Check if the ground block (Y-1) is solid
        var groundPos = new BlockPos(position.X, position.Y - 1, position.Z);
        var groundBlock = sapi.World.BlockAccessor.GetBlock(groundPos);

        // Ground must be solid for entity to stand on
        if (!groundBlock.SideSolid[BlockFacing.UP.Index] && !IsLiquidOrClimbable(groundBlock))
        {
            return false;
        }

        // Check clearance for entity base (Y)
        var basePos = new BlockPos(position.X, position.Y, position.Z);
        var baseBlock = sapi.World.BlockAccessor.GetBlock(basePos);

        // Entity base level must be clear
        if (baseBlock.Id != 0 && baseBlock.CollisionBoxes?.Length > 0)
        {
            return false;
        }

        // Check clearance for entity body (Y+1)
        var bodyPos = new BlockPos(position.X, position.Y + 1, position.Z);
        var bodyBlock = sapi.World.BlockAccessor.GetBlock(bodyPos);

        // Entity body level must be clear
        if (bodyBlock.Id != 0 && bodyBlock.CollisionBoxes?.Length > 0)
        {
            return false;
        }

        return true;
    }    /// <summary>
         /// Check if block is liquid or climbable
         /// </summary>
    private bool IsLiquidOrClimbable(Block block)
    {
        if (block == null) return false;

        var code = block.Code?.ToString();
        if (code == null) return false;

        return code.Contains("water", StringComparison.OrdinalIgnoreCase) ||
               code.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
               code.Contains("rope", StringComparison.OrdinalIgnoreCase) ||
               code.Contains("vine", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if diagonal movement is valid (prevents cutting corners through solid blocks)
    /// </summary>
    private bool IsValidDiagonalMovement(Vec3i from, Vec3i to)
    {
        // Check intermediate positions to prevent corner cutting
        var intermediatePos1 = new Vec3i(from.X, from.Y, to.Z);
        var intermediatePos2 = new Vec3i(to.X, from.Y, from.Z);

        // Both intermediate positions must be walkable
        return IsPositionWalkable(intermediatePos1) && IsPositionWalkable(intermediatePos2);
    }

    /// <summary>
    /// Check if floating is allowed at position (near other solid blocks)
    /// </summary>
    private bool IsFloatingAllowed(Vec3i position)
    {
        // Allow floating if there's a solid block adjacent
        var adjacentPositions = new[]
        {
            new BlockPos(position.X + 1, position.Y, position.Z),
            new BlockPos(position.X - 1, position.Y, position.Z),
            new BlockPos(position.X, position.Y, position.Z + 1),
            new BlockPos(position.X, position.Y, position.Z - 1)
        };

        foreach (var adjPos in adjacentPositions)
        {
            if (sapi.World.BlockAccessor.IsValidPos(adjPos))
            {
                var adjBlock = sapi.World.BlockAccessor.GetBlock(adjPos);
                if (adjBlock.SideSolid[BlockFacing.UP.Index])
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Calculate movement cost between two positions
    /// </summary>
    private float CalculateMoveCost(Vec3i from, Vec3i to)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = to.Y - from.Y; // Signed for jump detection
        var dz = Math.Abs(to.Z - from.Z);

        var baseCost = MOVE_COST_HORIZONTAL;

        // Diagonal movement
        if (dx + dz > 1)
        {
            baseCost = MOVE_COST_DIAGONAL;
        }

        // Vertical movement cost
        if (dy != 0)
        {
            baseCost += Math.Abs(dy) * MOVE_COST_VERTICAL;
        }

        // Jump penalty (upward movement)
        if (dy > 0)
        {
            baseCost += dy * JUMP_COST_PENALTY;
        }

        // Additional cost for risky moves (like drops)
        if (dy < -2)
        {
            baseCost += Math.Abs(dy) * 0.5f;
        }

        return baseCost;
    }

    /// <summary>
    /// Calculate heuristic distance (Manhattan distance with 3D adjustment)
    /// </summary>
    private float CalculateHeuristic(Vec3i from, Vec3i to)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = Math.Abs(to.Y - from.Y);
        var dz = Math.Abs(to.Z - from.Z);

        // 3D Manhattan distance with slight preference for horizontal movement
        return dx + dz + (dy * 1.2f);
    }

    /// <summary>
    /// Check if we're close enough to the destination
    /// </summary>
    private bool IsAtDestination(Vec3i current, Vec3i destination)
    {
        var distance = Math.Abs(current.X - destination.X) +
                      Math.Abs(current.Y - destination.Y) +
                      Math.Abs(current.Z - destination.Z);

        return distance <= 1; // Within 1 block is considered "at destination"
    }

    /// <summary>
    /// Reconstruct the path from goal to start
    /// </summary>
    private List<Vec3d> ReconstructPath(AStarNode goalNode)
    {
        var path = new List<Vec3d>();
        var currentNode = goalNode;

        while (currentNode != null)
        {
            // Convert to world coordinates (center of block)
            path.Add(new Vec3d(
                currentNode.Position.X + 0.5,
                currentNode.Position.Y,
                currentNode.Position.Z + 0.5
            ));

            currentNode = currentNode.Parent;
        }

        // Reverse to get path from start to goal
        path.Reverse();

        // Optimize path by removing unnecessary waypoints
        return OptimizePath(path);
    }

    /// <summary>
    /// Optimize path by removing unnecessary intermediate waypoints
    /// </summary>
    private List<Vec3d> OptimizePath(List<Vec3d> path)
    {
        if (path.Count <= 2)
            return path;

        var optimized = new List<Vec3d> { path[0] };
        var lastAddedIndex = 0;

        for (int i = 2; i < path.Count; i++)
        {
            // Check if we can move directly from last added waypoint to current position
            if (!CanMoveDirectly(path[lastAddedIndex], path[i]))
            {
                // Add the previous waypoint
                optimized.Add(path[i - 1]);
                lastAddedIndex = i - 1;
            }
        }

        // Always add the final destination
        optimized.Add(path.Last());

        return optimized;
    }

    /// <summary>
    /// Check if we can move directly between two points
    /// </summary>
    private bool CanMoveDirectly(Vec3d from, Vec3d to)
    {
        var distance = from.DistanceTo(to);
        if (distance > 5f) return false; // Don't optimize over long distances

        var direction = (to - from).Normalize();
        var steps = (int)(distance / 0.5f);

        for (int i = 1; i < steps; i++)
        {
            var checkPos = from + direction * (distance * i / steps);
            var blockPos = new Vec3i((int)checkPos.X, (int)checkPos.Y, (int)checkPos.Z);

            if (!IsPositionWalkable(blockPos))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get path to closest reachable position when full path is not possible
    /// </summary>
    private List<Vec3d> GetPathToClosestReachable(Vec3d start, Vec3d end, string entityId)
    {
        var path = new List<Vec3d>();

        // Find the closest node that was reached during search
        AStarNode? closestNode = null;
        float closestDistance = float.MaxValue;

        foreach (var kvp in openSet.Values.Concat(closedSet.Select(pos =>
            openSet.ContainsKey(pos) ? openSet[pos] : null).Where(n => n != null)))
        {
            if (kvp == null) continue;

            var nodePos = new Vec3d(kvp.Position.X + 0.5, kvp.Position.Y, kvp.Position.Z + 0.5);
            var distance = nodePos.DistanceTo(end);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestNode = kvp;
            }
        }

        if (closestNode != null)
        {
            var partialPath = ReconstructPath(closestNode);

            // Show path particles for partial path
            ShowPathParticles(partialPath, $"{entityId}_partial");

            return partialPath;
        }

        return path;
    }

    /// <summary>
    /// Find the nearest accessible position around a blocked target
    /// </summary>
    private Vec3i? FindNearestAccessiblePosition(Vec3i blockedTarget)
    {
        // Search in expanding radius around the blocked target
        for (int radius = 1; radius <= 5; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    // Only check positions on the edge of the current radius
                    if (Math.Abs(dx) != radius && Math.Abs(dz) != radius)
                        continue;

                    // Try different Y levels around the target
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        var testPos = new Vec3i(
                            blockedTarget.X + dx,
                            blockedTarget.Y + dy,
                            blockedTarget.Z + dz
                        );

                        if (IsPositionWalkable(testPos))
                        {
                            return testPos;
                        }
                    }
                }
            }
        }

        return null; // No accessible position found
    }

    /// <summary>
    /// Show path particles for visualization (if enabled)
    /// </summary>
    private void ShowPathParticles(List<Vec3d> path, string entityId)
    {
        if (debugVisualization == null || path == null || path.Count == 0)
            return;

        try
        {
            // Log start and end coordinates
            var startCoord = path.First();
            var endCoord = path.Last();

            DebugLogger.AIPath("AStar", "PathCoordinates",
                $"Entity {entityId}: START({startCoord.X:F1},{startCoord.Y:F1},{startCoord.Z:F1}) -> END({endCoord.X:F1},{endCoord.Y:F1},{endCoord.Z:F1})",
                $"Path length: {path.Count} waypoints");

            debugVisualization.ShowEntityPath(entityId, path);

            DebugLogger.AIPath("AStar", "ParticlesShown",
                $"Visualizing path with {path.Count} waypoints for entity: {entityId}",
                "Path particle visualization");
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"Failed to show path particles for entity {entityId}", ex);
        }
    }

    /// <summary>
    /// Clear path particles for a specific entity
    /// </summary>
    public void ClearPathParticles(string entityId)
    {
        debugVisualization?.ClearEntityPathParticles(entityId);
    }

    /// <summary>
    /// Clear all path particles
    /// </summary>
    public void ClearAllPathParticles()
    {
        debugVisualization?.ClearAllPathParticles();
    }

    /// <summary>
    /// Enable or disable path visualization
    /// </summary>
    public void SetPathVisualization(bool enabled)
    {
        debugVisualization?.EnablePathVisualization(enabled);
    }

    /// <summary>
    /// Refresh path particles (call periodically to maintain visibility)
    /// </summary>
    public void RefreshPathParticles()
    {
        debugVisualization?.RefreshParticles();
    }

    /// <summary>
    /// Get valid neighboring positions in 3D space with proper diagonal movement validation
    /// </summary>
    private IEnumerable<Vec3i> GetValidNeighbors(Vec3i position)
    {
        // Define movement directions: 4 cardinal + 4 diagonal + up/down variations
        var directions = new Vec3i[]
        {
            // Cardinal directions (safe)
            new Vec3i(-1,  0,  0), // West
            new Vec3i( 1,  0,  0), // East
            new Vec3i( 0,  0, -1), // North
            new Vec3i( 0,  0,  1), // South

            // Diagonal directions (need corner checking)
            new Vec3i(-1,  0, -1), // Northwest
            new Vec3i( 1,  0, -1), // Northeast
            new Vec3i(-1,  0,  1), // Southwest
            new Vec3i( 1,  0,  1), // Southeast

            // Vertical movement
            new Vec3i( 0,  1,  0), // Up
            new Vec3i( 0, -1,  0), // Down

            // Diagonal with vertical (climbing/descending diagonally)
            new Vec3i(-1,  1,  0), // West-Up
            new Vec3i( 1,  1,  0), // East-Up
            new Vec3i( 0,  1, -1), // North-Up
            new Vec3i( 0,  1,  1), // South-Up
            new Vec3i(-1, -1,  0), // West-Down
            new Vec3i( 1, -1,  0), // East-Down
            new Vec3i( 0, -1, -1), // North-Down
            new Vec3i( 0, -1,  1)  // South-Down
        };

        foreach (var direction in directions)
        {
            var neighbor = new Vec3i(
                position.X + direction.X,
                position.Y + direction.Y,
                position.Z + direction.Z
            );

            // Check if the movement is valid
            if (!IsValidMovement(position, neighbor))
                continue;

            // For diagonal movement, check if corners are clear
            if (IsDiagonalMovement(direction) && !IsDiagonalMovementValid(position, neighbor))
                continue;

            yield return neighbor;
        }
    }

    /// <summary>
    /// Check if a movement direction is diagonal
    /// </summary>
    private bool IsDiagonalMovement(Vec3i direction)
    {
        // Diagonal if moving in both X and Z directions
        return Math.Abs(direction.X) + Math.Abs(direction.Z) > 1;
    }

    /// <summary>
    /// Validate diagonal movement to prevent corner-cutting through solid blocks
    /// </summary>
    private bool IsDiagonalMovementValid(Vec3i from, Vec3i to)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;

        // Only check for horizontal diagonal movement
        if (Math.Abs(dx) != 1 || Math.Abs(dz) != 1)
            return true; // Not a horizontal diagonal

        // Check the two corner blocks that could block the diagonal movement
        var corner1 = new Vec3i(from.X + dx, from.Y, from.Z);     // X-direction first
        var corner2 = new Vec3i(from.X, from.Y, from.Z + dz);     // Z-direction first

        // Both corners must be passable for diagonal movement
        return IsPositionWalkable(corner1) && IsPositionWalkable(corner2);
    }
}

/// <summary>
/// A* node for pathfinding
/// </summary>
public class AStarNode : IComparable<AStarNode>
{
    public Vec3i Position { get; set; }
    public float GCost { get; set; } // Distance from start
    public float HCost { get; set; } // Heuristic distance to goal
    public float FCost => GCost + HCost; // Total cost
    public AStarNode? Parent { get; set; }

    public int CompareTo(AStarNode? other)
    {
        if (other == null) return -1;

        var fComparison = FCost.CompareTo(other.FCost);
        if (fComparison != 0) return fComparison;

        // Tie-breaker: prefer lower H cost (closer to goal)
        return HCost.CompareTo(other.HCost);
    }
}

/// <summary>
/// Priority queue for A* pathfinding
/// </summary>
public class PriorityQueue<T> where T : IComparable<T>
{
    private readonly List<T> items = new();

    public int Count => items.Count;

    public void Enqueue(T item)
    {
        items.Add(item);

        // Bubble up
        var childIndex = items.Count - 1;
        while (childIndex > 0)
        {
            var parentIndex = (childIndex - 1) / 2;
            if (items[childIndex].CompareTo(items[parentIndex]) >= 0)
                break;

            // Swap
            (items[childIndex], items[parentIndex]) = (items[parentIndex], items[childIndex]);
            childIndex = parentIndex;
        }
    }

    public T Dequeue()
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Queue is empty");

        var result = items[0];
        var lastIndex = items.Count - 1;
        items[0] = items[lastIndex];
        items.RemoveAt(lastIndex);

        // Bubble down
        if (items.Count > 0)
        {
            var parentIndex = 0;
            while (true)
            {
                var leftChild = parentIndex * 2 + 1;
                var rightChild = parentIndex * 2 + 2;
                var smallest = parentIndex;

                if (leftChild < items.Count && items[leftChild].CompareTo(items[smallest]) < 0)
                    smallest = leftChild;

                if (rightChild < items.Count && items[rightChild].CompareTo(items[smallest]) < 0)
                    smallest = rightChild;

                if (smallest == parentIndex)
                    break;

                // Swap
                (items[parentIndex], items[smallest]) = (items[smallest], items[parentIndex]);
                parentIndex = smallest;
            }
        }

        return result;
    }

    public void Clear()
    {
        items.Clear();
    }
}
