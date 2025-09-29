using System;
using System.Collections.Generic;
using HueHordes.Debug;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Manages health and concurrent attackers for doors and gates
/// </summary>
public class DoorHealthManager
{
    private readonly ICoreServerAPI sapi;
    private readonly Dictionary<Vec3i, DoorInfo> doorHealth = new();

    public DoorHealthManager(ICoreServerAPI serverApi)
    {
        sapi = serverApi ?? throw new ArgumentNullException(nameof(serverApi));
    }

    /// <summary>
    /// Try to register an attacker for a door. Returns false if door is at max attackers.
    /// </summary>
    public bool TryRegisterAttacker(Vec3d doorPos, long entityId)
    {
        var blockPos = new Vec3i((int)doorPos.X, (int)doorPos.Y, (int)doorPos.Z);

        if (!doorHealth.TryGetValue(blockPos, out var doorInfo))
        {
            doorInfo = new DoorInfo
            {
                Position = blockPos,
                MaxHealth = 2000f,
                CurrentHealth = 2000f,
                MaxAttackers = 3
            };
            doorHealth[blockPos] = doorInfo;
        }

        // Check if entity is already attacking this door
        if (doorInfo.CurrentAttackers.Contains(entityId))
            return true;

        // Check if we can add more attackers
        if (doorInfo.CurrentAttackers.Count >= doorInfo.MaxAttackers)
        {
            DebugLogger.AIEvent("Door attack denied",
                $"Max attackers ({doorInfo.MaxAttackers}) reached for door at {doorPos.X:F1},{doorPos.Y:F1},{doorPos.Z:F1}",
                entityId.ToString());
            return false;
        }

        // Register the attacker
        doorInfo.CurrentAttackers.Add(entityId);
        DebugLogger.AIEvent("Door attacker registered",
            $"Entity {entityId} attacking door ({doorInfo.CurrentAttackers.Count}/{doorInfo.MaxAttackers})",
            entityId.ToString());

        return true;
    }

    /// <summary>
    /// Unregister an attacker from a door
    /// </summary>
    public void UnregisterAttacker(Vec3d doorPos, long entityId)
    {
        var blockPos = new Vec3i((int)doorPos.X, (int)doorPos.Y, (int)doorPos.Z);

        if (doorHealth.TryGetValue(blockPos, out var doorInfo))
        {
            doorInfo.CurrentAttackers.Remove(entityId);
            DebugLogger.AIEvent("Door attacker unregistered",
                $"Entity {entityId} stopped attacking door ({doorInfo.CurrentAttackers.Count}/{doorInfo.MaxAttackers})",
                entityId.ToString());
        }
    }

    /// <summary>
    /// Attack a door and return true if door should be destroyed
    /// </summary>
    public bool AttackDoor(Vec3d doorPos, long entityId, float damage = 50f)
    {
        var blockPos = new Vec3i((int)doorPos.X, (int)doorPos.Y, (int)doorPos.Z);

        if (!doorHealth.TryGetValue(blockPos, out var doorInfo))
            return false;

        // Only registered attackers can damage the door
        if (!doorInfo.CurrentAttackers.Contains(entityId))
            return false;

        // Apply damage
        doorInfo.CurrentHealth -= damage;
        doorInfo.LastAttackTime = sapi.World.ElapsedMilliseconds;

        DebugLogger.AIEvent("Door attacked",
            $"Damage: {damage}, Health: {doorInfo.CurrentHealth:F0}/{doorInfo.MaxHealth:F0}",
            entityId.ToString());

        // Check if door should be destroyed
        if (doorInfo.CurrentHealth <= 0)
        {
            DebugLogger.AIEvent("Door destroyed",
                $"Door at {doorPos.X:F1},{doorPos.Y:F1},{doorPos.Z:F1} destroyed after {doorInfo.MaxHealth - doorInfo.CurrentHealth:F0} damage",
                entityId.ToString());

            // Remove from tracking
            doorHealth.Remove(blockPos);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get door info for debugging/display
    /// </summary>
    public DoorInfo? GetDoorInfo(Vec3d doorPos)
    {
        var blockPos = new Vec3i((int)doorPos.X, (int)doorPos.Y, (int)doorPos.Z);
        return doorHealth.TryGetValue(blockPos, out var info) ? info : null;
    }

    /// <summary>
    /// Clean up doors that no longer exist or haven't been attacked recently
    /// </summary>
    public void CleanupOldDoors()
    {
        var currentTime = sapi.World.ElapsedMilliseconds;
        var toRemove = new List<Vec3i>();

        foreach (var kvp in doorHealth)
        {
            var doorInfo = kvp.Value;

            // Remove if no attackers for 30 seconds
            if (doorInfo.CurrentAttackers.Count == 0 &&
                (currentTime - doorInfo.LastAttackTime) > 30000)
            {
                toRemove.Add(kvp.Key);
                continue;
            }

            // Check if door block still exists
            var blockPos = new BlockPos(kvp.Key.X, kvp.Key.Y, kvp.Key.Z);
            if (sapi.World.BlockAccessor.IsValidPos(blockPos))
            {
                var block = sapi.World.BlockAccessor.GetBlock(blockPos);
                var blockCode = block.Code?.ToString();

                if (blockCode == null ||
                    (!blockCode.Contains("door", StringComparison.OrdinalIgnoreCase) &&
                     !blockCode.Contains("gate", StringComparison.OrdinalIgnoreCase)))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            else
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var pos in toRemove)
        {
            doorHealth.Remove(pos);
        }

        if (toRemove.Count > 0)
        {
            DebugLogger.Event("Door cleanup", $"Removed {toRemove.Count} stale door entries");
        }
    }
}

/// <summary>
/// Information about a door being attacked
/// </summary>
public class DoorInfo
{
    public Vec3i Position { get; set; }
    public float MaxHealth { get; set; } = 2000f;
    public float CurrentHealth { get; set; } = 2000f;
    public int MaxAttackers { get; set; } = 3;
    public HashSet<long> CurrentAttackers { get; set; } = new();
    public long LastAttackTime { get; set; }

    public float HealthPercentage => CurrentHealth / MaxHealth;
}
