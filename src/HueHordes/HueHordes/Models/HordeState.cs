namespace HueHordes.Models;

/// <summary>
/// Represents the horde timing state for an individual player.
/// Tracks when the player last experienced a horde event.
/// </summary>
public class HordeState
{
    /// <summary>
    /// The total in-game days when this player last had a horde spawned.
    /// Used to calculate when the next horde should occur.
    /// </summary>
    public double LastHordeTotalDays { get; set; }
}