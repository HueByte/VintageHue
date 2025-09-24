using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.Models;

/// <summary>
/// Represents a target for horde AI with priority and behavior information
/// </summary>
public class HordeTarget
{
    /// <summary>
    /// Target position in the world
    /// </summary>
    public Vec3d Position { get; set; } = Vec3d.Zero;

    /// <summary>
    /// Type of target this represents
    /// </summary>
    public TargetType Type { get; set; }

    /// <summary>
    /// Priority of this target (higher = more important)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Entity reference if target is a living entity
    /// </summary>
    public EntityAgent? TargetEntity { get; set; }

    /// <summary>
    /// Player reference if target is a player
    /// </summary>
    public IServerPlayer? TargetPlayer { get; set; }

    /// <summary>
    /// Base reference if target is related to a base
    /// </summary>
    public PlayerBase? RelatedBase { get; set; }

    /// <summary>
    /// When this target was last seen/updated
    /// </summary>
    public double LastSeenTime { get; set; }

    /// <summary>
    /// How long this target remains valid (in game ticks)
    /// </summary>
    public float ValidityDuration { get; set; } = 1000f;

    /// <summary>
    /// Whether this target is currently visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Distance from the pursuing entity
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Custom data for specific target behaviors
    /// </summary>
    public string? CustomData { get; set; }

    /// <summary>
    /// Check if this target is still valid
    /// </summary>
    public bool IsValid(double currentTime)
    {
        if (Type == TargetType.Player && TargetPlayer != null)
        {
            // Player targets are valid as long as player is online and alive
            return TargetPlayer.ConnectionState == EnumClientState.Playing &&
                   TargetPlayer.Entity?.Alive == true;
        }

        if (Type == TargetType.Entity && TargetEntity != null)
        {
            // Entity targets are valid while entity is alive
            return TargetEntity.Alive;
        }

        // Static targets (base center, entrance) are valid for their duration
        return (currentTime - LastSeenTime) < ValidityDuration;
    }

    /// <summary>
    /// Update target position from its source
    /// </summary>
    public void UpdatePosition(double currentTime)
    {
        LastSeenTime = currentTime;

        switch (Type)
        {
            case TargetType.Player when TargetPlayer?.Entity != null:
                Position = TargetPlayer.Entity.ServerPos.XYZ.Clone();
                IsVisible = true;
                break;

            case TargetType.Entity when TargetEntity != null:
                Position = TargetEntity.ServerPos.XYZ.Clone();
                IsVisible = true;
                break;

            case TargetType.BaseCenter:
            case TargetType.BaseEntrance:
            case TargetType.PatrolPoint:
                // Static targets don't move
                IsVisible = true;
                break;
        }
    }

    /// <summary>
    /// Calculate priority score based on distance and type
    /// </summary>
    public int CalculateDynamicPriority(Vec3d fromPosition)
    {
        int basePriority = Priority;
        double distance = fromPosition.SquareDistanceTo(Position);

        // Closer targets get higher priority (within reason)
        if (distance < 100) // Within 10 blocks
            basePriority += 20;
        else if (distance < 400) // Within 20 blocks
            basePriority += 10;
        else if (distance > 2500) // Beyond 50 blocks
            basePriority -= 10;

        // Player targets get bonus if they're moving (more engaging)
        if (Type == TargetType.Player && TargetPlayer?.Entity != null)
        {
            var velocity = TargetPlayer.Entity.ServerPos.Motion;
            if (velocity.Length() > 0.1) // Player is moving
                basePriority += 15;
        }

        return basePriority;
    }
}

/// <summary>
/// Types of targets that horde AI can pursue
/// </summary>
public enum TargetType
{
    /// <summary>
    /// A player entity (highest priority)
    /// </summary>
    Player = 100,

    /// <summary>
    /// Another living entity
    /// </summary>
    Entity = 80,

    /// <summary>
    /// Base entrance/gate
    /// </summary>
    BaseEntrance = 60,

    /// <summary>
    /// Center of player base
    /// </summary>
    BaseCenter = 40,

    /// <summary>
    /// Patrol point within base
    /// </summary>
    PatrolPoint = 20,

    /// <summary>
    /// Last known position of lost target
    /// </summary>
    LastKnownPosition = 10
}