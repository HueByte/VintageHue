namespace HueHordes;

public class ServerConfig
{
    /// <summary>
    /// In-game days between horde events.
    /// </summary>
    public int DaysBetweenHordes { get; set; } = 3;

    /// <summary>
    /// Number of mobs per horde.
    /// </summary>
    public int Count { get; set; } = 8;

    /// <summary>
    /// Minimum ring spawn radius.
    /// </summary>
    public float SpawnRadiusMin { get; set; } = 12f;

    /// <summary>
    /// Maximum ring spawn radius.
    /// </summary>
    public float SpawnRadiusMax { get; set; } = 24f;

    /// <summary>
    /// Supply full or domain-less entity codes.
    /// </summary>
    public string[] EntityCodes { get; set; } = new[] { "drifter-normal" };

    /// <summary>
    /// Enables simple "move toward" behavior.
    /// </summary>
    public bool NudgeTowardInitialPos { get; set; } = true;

    /// <summary>
    /// How long to nudge mobs.
    /// </summary>
    public float NudgeSeconds { get; set; } = 20f;

    /// <summary>
    /// Motion per tick (e.g., ~0.05–0.12).
    /// </summary>
    public float NudgeSpeed { get; set; } = 0.05f;
}