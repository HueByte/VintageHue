using HueHordes.AI;
using HueHordes.Debug;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes;

/// <summary>
/// Main mod system that integrates the AI horde system.
/// Provides commands and configuration for the clean AI implementation.
/// </summary>
public class HueHordesModSystem : ModSystem
{
    private ICoreServerAPI? sapi;
    private HordeSystem? newHordeSystem;
    private static ServerConfig? config;

    const string ConfigFile = "Horde.server.json";

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        // Load config (create defaults if none)
        try
        {
            config = sapi.LoadModConfig<ServerConfig>(ConfigFile) ?? new ServerConfig();
            sapi.StoreModConfig(config, ConfigFile);

            // Initialize debug logging system
            DebugLogger.Initialize(sapi, config.EnableDebugLogging, config.DebugLoggingLevel);
            DebugLogger.Event("Mod system starting", $"Debug: {config.EnableDebugLogging}, Level: {config.DebugLoggingLevel}");
        }
        catch
        {
            config = new ServerConfig();
            DebugLogger.Initialize(sapi, false, 1);
        }

        // Initialize new clean simple system
        try
        {
            newHordeSystem = new HordeSystem(sapi);
            DebugLogger.Event("AI system initialized", "Clean spawning and base detection ready");
        }
        catch (System.Exception ex)
        {
            sapi.Logger.Error("HueHordes: Failed to initialize AI system: " + ex.Message);
        }
    }

    public override void Dispose()
    {
        newHordeSystem = null;
        sapi = null;
    }
}
