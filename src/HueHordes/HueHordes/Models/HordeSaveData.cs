using System.Collections.Generic;

namespace HueHordes.Models;

/// <summary>
/// Save data persistence model that stores horde timing information for all players.
/// This data is saved to the world save file to track when each player last had a horde.
/// </summary>
public class HordeSaveData
{
    /// <summary>
    /// Dictionary mapping player UIDs to their individual horde states.
    /// </summary>
    public Dictionary<string, HordeState> ByPlayerUid { get; set; } = new();
}