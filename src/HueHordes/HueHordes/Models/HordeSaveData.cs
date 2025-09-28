using System.Collections.Generic;

namespace HueHordes.Models;

public class HordeSaveData
{
    public Dictionary<string, HordeState> ByPlayerUid { get; set; } = new();
}