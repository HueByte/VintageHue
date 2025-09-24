using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HueHordes.AI;
using HueHordes.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace HueHordes;

public class HordeSystem : ModSystem
{
    ICoreServerAPI? sapi;
    long listenerId;
    static ServerConfig? config;
    HordeSaveData save = new();
    HordeAI? hordeAI; // Legacy AI system
    AsyncHordeAI? asyncHordeAI; // Modern async AI system
    AsyncConfigurationManager? configManager; // Async configuration management

    const string ConfigFile = "Horde.server.json";
    const string SaveKey = "horde:save";

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        // Load config (create defaults if none)
        try
        {
            config = sapi.LoadModConfig<ServerConfig>(ConfigFile) ?? new ServerConfig();
            sapi.StoreModConfig(config, ConfigFile);
        }
        catch
        {
            config = new ServerConfig();
        }

        // Initialize async configuration management
        configManager = new AsyncConfigurationManager(sapi);
        _ = Task.Run(async () =>
        {
            await configManager.InitializeAsync();
            config = configManager.GetCurrentConfiguration();
        });

        // Initialize advanced AI systems
        hordeAI = new HordeAI(sapi); // Legacy system for compatibility
        asyncHordeAI = new AsyncHordeAI(sapi); // Modern async system

        // Load save data
        sapi.Event.SaveGameLoaded += OnSaveLoaded;
        sapi.Event.GameWorldSave += OnSaveGame;

        // Tick every ~5 seconds
        listenerId = sapi.Event.RegisterGameTickListener(OnGameTick, 5000);

        // Horde management commands for server administrators
        var hordeCmd = sapi.ChatCommands
            .Create("horde")
            .RequiresPrivilege(Privilege.controlserver)
            .WithDescription("Manage the horde spawning system. Controls periodic spawning of hostile entities around players.");

        hordeCmd.BeginSubCommand("now")
            .WithDescription("Immediately spawn a horde around your current position. Useful for testing horde mechanics and entity spawning.")
            .HandleWith(OnHordeNowCmd);

        hordeCmd.BeginSubCommand("reset")
            .WithDescription("Reset all player horde timers to zero. This will cause all online players to be eligible for hordes on the next tick cycle.")
            .HandleWith(OnHordeResetCmd);

        hordeCmd.BeginSubCommand("setdays")
            .WithDescription("Configure the interval between horde events. Sets how many in-game days must pass before a player becomes eligible for another horde.")
            .WithArgs(sapi.ChatCommands.Parsers.Int("days"))
            .HandleWith(OnHordeSetDaysCmd);

        hordeCmd.BeginSubCommand("status")
            .WithDescription("Display comprehensive horde system information including current configuration, game time, and all online players' horde timer status.")
            .HandleWith(OnHordeStatusCmd);

        hordeCmd.BeginSubCommand("spawn")
            .WithDescription("Force spawn a horde for a specific online player. The target player's horde timer will be updated to prevent immediate respawning.")
            .WithArgs(sapi.ChatCommands.Parsers.Word("playername"))
            .HandleWith(OnHordeSpawnCmd);

        hordeCmd.BeginSubCommand("aiinfo")
            .WithDescription("Show information about the AI system including detected player bases.")
            .HandleWith(OnHordeAIInfoCmd);

        hordeCmd.BeginSubCommand("refreshbase")
            .WithDescription("Force refresh base detection for a specific player.")
            .WithArgs(sapi.ChatCommands.Parsers.Word("playername"))
            .HandleWith(OnHordeRefreshBaseCmd);
    }

    public override void Dispose()
    {
        if (listenerId != 0) sapi?.Event.UnregisterGameTickListener(listenerId);

        // Dispose async resources
        asyncHordeAI?.Dispose();
        configManager?.Dispose();

        base.Dispose();
    }

    void OnSaveLoaded()
    {
        // Pull our last-run data from the save
        var got = sapi.WorldManager.SaveGame.GetData<HordeSaveData>(SaveKey);
        if (got != null) save = got;
    }

    void OnSaveGame()
    {
        sapi.WorldManager.SaveGame.StoreData(SaveKey, save);
    }

    void OnGameTick(float dt)
    {
        var cal = sapi.World.Calendar; // IGameCalendar (TotalDays, etc.)
        double today = cal.TotalDays;

        foreach (var plr in sapi.World.AllOnlinePlayers) // IWorldAccessor player list
        {
            if (plr?.PlayerUID == null) continue;
            if (!save.ByPlayerUid.TryGetValue(plr.PlayerUID, out var st))
            {
                st = new HordeState { LastHordeTotalDays = today };
                save.ByPlayerUid[plr.PlayerUID] = st;
                continue;
            }

            var due = (today - st.LastHordeTotalDays) >= config!.DaysBetweenHordes;
            if (!due) continue;

            _ = SpawnSmartHordeForAsync((IServerPlayer)plr, today);
            st.LastHordeTotalDays = today;
        }
    }

    TextCommandResult OnHordeNowCmd(TextCommandCallingArgs args)
    {
        var sp = args.Caller as IServerPlayer;
        if (sp == null) return TextCommandResult.Error("This command can only be used by players.");

        _ = SpawnSmartHordeForAsync(sp, sapi!.World.Calendar.TotalDays);
        return TextCommandResult.Success("Horde spawned for you!");
    }

    TextCommandResult OnHordeResetCmd(TextCommandCallingArgs args)
    {
        int playerCount = save.ByPlayerUid.Count;
        save.ByPlayerUid.Clear();
        return TextCommandResult.Success($"Horde timers reset for {playerCount} players.");
    }

    TextCommandResult OnHordeSetDaysCmd(TextCommandCallingArgs args)
    {
        int days = (int)args.Parsers[0].GetValue();
        int oldDays = config!.DaysBetweenHordes;

        config.DaysBetweenHordes = Math.Max(1, days);
        sapi!.StoreModConfig(config, ConfigFile);

        return TextCommandResult.Success($"Days between hordes changed from {oldDays} to {config.DaysBetweenHordes}");
    }

    TextCommandResult OnHordeStatusCmd(TextCommandCallingArgs args)
    {
        var cal = sapi!.World.Calendar;
        double currentDay = cal.TotalDays;

        var status = new System.Text.StringBuilder();
        status.AppendLine($"=== Horde Configuration ===");
        status.AppendLine($"Days between hordes: {config!.DaysBetweenHordes}");
        status.AppendLine($"Entities per horde: {config.Count}");
        status.AppendLine($"Spawn radius: {config.SpawnRadiusMin}-{config.SpawnRadiusMax}");
        status.AppendLine($"Entity types: {string.Join(", ", config.EntityCodes)}");
        status.AppendLine($"Current game day: {currentDay:F2}");
        status.AppendLine();

        status.AppendLine($"=== Player Status ===");
        var onlinePlayers = sapi.World.AllOnlinePlayers.Where(p => p?.PlayerUID != null).ToList();

        if (!onlinePlayers.Any())
        {
            status.AppendLine("No online players.");
        }
        else
        {
            foreach (var player in onlinePlayers)
            {
                if (save.ByPlayerUid.TryGetValue(player.PlayerUID, out var state))
                {
                    double daysSinceLastHorde = currentDay - state.LastHordeTotalDays;
                    double daysUntilNext = config.DaysBetweenHordes - daysSinceLastHorde;
                    string nextHordeInfo = daysUntilNext <= 0 ? "DUE NOW" : $"in {daysUntilNext:F1} days";

                    status.AppendLine($"  {player.PlayerName}: Last horde {daysSinceLastHorde:F1} days ago, next {nextHordeInfo}");
                }
                else
                {
                    status.AppendLine($"  {player.PlayerName}: No horde history (will trigger on next tick)");
                }
            }
        }

        return TextCommandResult.Success(status.ToString());
    }

    TextCommandResult OnHordeSpawnCmd(TextCommandCallingArgs args)
    {
        string playerName = (string)args.Parsers[0].GetValue();
        var targetPlayer = sapi!.World.AllOnlinePlayers
            .FirstOrDefault(p => p?.PlayerName?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true) as IServerPlayer;

        if (targetPlayer == null)
            return TextCommandResult.Error($"Player '{playerName}' not found or not online.");

        _ = SpawnSmartHordeForAsync(targetPlayer, sapi.World.Calendar.TotalDays);

        // Update their timer so they don't get another horde immediately
        if (!save.ByPlayerUid.TryGetValue(targetPlayer.PlayerUID, out var state))
        {
            state = new HordeState();
            save.ByPlayerUid[targetPlayer.PlayerUID] = state;
        }
        state.LastHordeTotalDays = sapi.World.Calendar.TotalDays;

        return TextCommandResult.Success($"Horde spawned for player {targetPlayer.PlayerName}!");
    }

    TextCommandResult OnHordeAIInfoCmd(TextCommandCallingArgs args)
    {
        if (asyncHordeAI == null && hordeAI == null)
            return TextCommandResult.Error("AI systems not initialized.");

        // Start async operation to get comprehensive stats
        _ = Task.Run(async () =>
        {
            try
            {
                var info = new System.Text.StringBuilder();
                info.AppendLine("=== Horde AI System Information ===");

                if (asyncHordeAI != null)
                {
                    var asyncStats = await asyncHordeAI.GetAIStatsAsync();
                    info.AppendLine("Modern Async AI: Active");
                    info.AppendLine(asyncStats);
                }

                if (hordeAI != null)
                {
                    info.AppendLine("Legacy AI: Active (Fallback)");
                    info.AppendLine(hordeAI.GetAIStats());
                }

                if (configManager != null)
                {
                    var configStats = await configManager.GetConfigurationStatsAsync();
                    info.AppendLine(configStats);
                }

                // Send the comprehensive info to the command caller
                if (args.Caller is IServerPlayer player)
                {
                    sapi!.SendMessage(player, 0, info.ToString(), EnumChatType.CommandSuccess);
                }
            }
            catch (Exception ex)
            {
                sapi!.Logger.Error($"[HordeSystem] Error getting AI info: {ex.Message}");
                if (args.Caller is IServerPlayer player)
                {
                    sapi.SendMessage(player, 0, $"Error getting AI info: {ex.Message}", EnumChatType.CommandError);
                }
            }
        });

        return TextCommandResult.Success("Gathering AI system information...");
    }

    TextCommandResult OnHordeAIInfoCmd_Legacy(TextCommandCallingArgs args)
    {
        if (hordeAI == null)
            return TextCommandResult.Error("Legacy AI system not initialized.");

        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Legacy Horde AI System Information ===");
        info.AppendLine($"Status: Active");
        info.AppendLine();

        // Show base information for all online players
        foreach (var player in sapi!.World.AllOnlinePlayers.OfType<IServerPlayer>())
        {
            var playerBase = hordeAI.GetPlayerBaseInfo(player.PlayerUID);
            if (playerBase != null)
            {
                info.AppendLine($"Player: {player.PlayerName}");
                info.AppendLine($"  Base Type: {playerBase.Type}");
                info.AppendLine($"  Base Center: {playerBase.Center.X:F1}, {playerBase.Center.Y:F1}, {playerBase.Center.Z:F1}");
                info.AppendLine($"  Bed Position: {(playerBase.BedPosition != null ? $"{playerBase.BedPosition.X:F1}, {playerBase.BedPosition.Y:F1}, {playerBase.BedPosition.Z:F1}" : "None")}");
                info.AppendLine($"  Has Enclosure: {playerBase.HasEnclosure}");
                info.AppendLine($"  Entrances: {playerBase.Entrances.Count}");
                info.AppendLine($"  Confidence: {playerBase.DetectionConfidence:P0}");
                info.AppendLine($"  Last Updated: {playerBase.LastDetectedDay:F2} days ago");
                info.AppendLine();
            }
            else
            {
                info.AppendLine($"Player: {player.PlayerName} - No base detected");
                info.AppendLine();
            }
        }

        return TextCommandResult.Success(info.ToString());
    }

    TextCommandResult OnHordeRefreshBaseCmd(TextCommandCallingArgs args)
    {
        string playerName = (string)args.Parsers[0].GetValue();
        var targetPlayer = sapi!.World.AllOnlinePlayers
            .FirstOrDefault(p => p?.PlayerName?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true) as IServerPlayer;

        if (targetPlayer == null)
            return TextCommandResult.Error($"Player '{playerName}' not found or not online.");

        // Start async base refresh operation
        _ = Task.Run(async () =>
        {
            try
            {
                PlayerBase? refreshedBase = null;

                if (asyncHordeAI != null)
                {
                    // Use modern async AI system
                    refreshedBase = await asyncHordeAI.RefreshPlayerBaseAsync(targetPlayer);
                }
                else if (hordeAI != null)
                {
                    // Fallback to legacy AI system
                    refreshedBase = await Task.Run(() => hordeAI.RefreshPlayerBase(targetPlayer));
                }

                // Send result back to command caller
                if (args.Caller is IServerPlayer caller)
                {
                    if (refreshedBase != null)
                    {
                        var message = $"Base refreshed for {targetPlayer.PlayerName}:\n" +
                                     $"Type: {refreshedBase.Type}, Confidence: {refreshedBase.DetectionConfidence:P0}\n" +
                                     $"Center: {refreshedBase.Center.X:F1}, {refreshedBase.Center.Y:F1}, {refreshedBase.Center.Z:F1}\n" +
                                     $"Entrances: {refreshedBase.Entrances.Count}, Has Enclosure: {refreshedBase.HasEnclosure}";
                        sapi!.SendMessage(caller, 0, message, EnumChatType.CommandSuccess);
                    }
                    else
                    {
                        sapi!.SendMessage(caller, 0, $"Failed to detect base for player {targetPlayer.PlayerName}", EnumChatType.CommandError);
                    }
                }
            }
            catch (Exception ex)
            {
                sapi!.Logger.Error($"[HordeSystem] Error refreshing base: {ex.Message}");
                if (args.Caller is IServerPlayer caller)
                {
                    sapi.SendMessage(caller, 0, $"Error refreshing base: {ex.Message}", EnumChatType.CommandError);
                }
            }
        });

        return TextCommandResult.Success($"Refreshing base detection for {targetPlayer.PlayerName}...");
    }

    async Task SpawnSmartHordeForAsync(IServerPlayer player, double today)
    {
        try
        {
            if (asyncHordeAI != null)
            {
                // Use modern async AI system for spawning
                await asyncHordeAI.SpawnSmartHordeAsync(player, config!, today);
                sapi!.SendMessage(player, 0, Lang.Get("Async smart horde incoming!"), EnumChatType.Notification);
            }
            else if (hordeAI != null)
            {
                // Fallback to legacy AI system
                await Task.Run(() => hordeAI.SpawnSmartHorde(player, config!, today));
                sapi!.SendMessage(player, 0, Lang.Get("Smart horde incoming!"), EnumChatType.Notification);
            }
            else
            {
                // Final fallback to simple spawning
                SpawnHordeFor(player, today);
            }
        }
        catch (Exception ex)
        {
            sapi!.Logger.Error($"[HordeSystem] Error in async spawn: {ex.Message}");
            // Fallback to simple spawning on error
            SpawnHordeFor(player, today);
        }
    }

    void SpawnHordeFor(IServerPlayer player, double today)
    {
        // Legacy simple spawning method (fallback)
        var initial = player.Entity.ServerPos.XYZ.Clone();

        var rnd = sapi!.World.Rand;
        for (int i = 0; i < config!.Count; i++)
        {
            var code = config!.EntityCodes[i % config.EntityCodes.Length];
            var pos = RandomPosAround(initial, rnd);

            TrySpawn(code, pos, initial);
        }

        sapi.SendMessage(player, 0, Lang.Get("Horde incoming!"), EnumChatType.Notification);
    }

    Vec3d RandomPosAround(Vec3d center, Random rnd)
    {
        // pick random polar coords in [min,max] radius ring
        double ang = rnd.NextDouble() * GameMath.TWOPI;
        double r = GameMath.Lerp(config!.SpawnRadiusMin, config.SpawnRadiusMax, (float)rnd.NextDouble());
        double x = center.X + Math.Cos(ang) * r;
        double z = center.Z + Math.Sin(ang) * r;

        // find a reasonable Y (surface-ish)
        int y = sapi.World.BlockAccessor.GetTerrainMapheightAt(new BlockPos((int)x, 0, (int)z));
        return new Vec3d(x + 0.5, y + 1, z + 0.5);
    }

    void TrySpawn(string entityCode, Vec3d spawnAt, Vec3d initialPlayerPos)
    {
        var loc = new AssetLocation(entityCode);
        if (string.IsNullOrEmpty(loc.Domain))
            loc = new AssetLocation("game", loc.Path);
        var etype = sapi.World.GetEntityType(loc);
        if (etype == null) { sapi.Logger.Warning($"[Horde] Unknown entity '{entityCode}'"); return; }

        var entity = sapi.World.ClassRegistry.CreateEntity(etype);
        entity.ServerPos.SetPos(spawnAt);
        entity.Pos.SetFrom(entity.ServerPos);

        // Spawn the entity first
        sapi.World.SpawnEntity(entity);

        // Tag with our target and optional nudge parameters after spawning
        entity.WatchedAttributes.SetDouble("hordeTargetX", initialPlayerPos.X);
        entity.WatchedAttributes.SetDouble("hordeTargetY", initialPlayerPos.Y);
        entity.WatchedAttributes.SetDouble("hordeTargetZ", initialPlayerPos.Z);
        entity.WatchedAttributes.SetFloat("hordeNudgeTime", config!.NudgeSeconds);
        entity.WatchedAttributes.SetFloat("hordeNudgeSpeed", config.NudgeSpeed);

        // Optional: add a tiny behavior that moves toward initial position for a bit
        if (config!.NudgeTowardInitialPos && entity is EntityAgent)
        {
            try
            {
                var behavior = new HordeNudgeBehavior(entity);
                entity.AddBehavior(behavior);
            }
            catch (Exception ex)
            {
                sapi!.Logger.Warning($"[Horde] Failed to add nudge behavior to entity {entityCode}: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// Minimal, non-pathfinding nudge toward a target point for N seconds.
/// This does not replace AI; it only sets a motion vector toward the point
/// and then disables itself. Replace with your own task/AI later if desired.
/// </summary>
public class HordeNudgeBehavior : EntityBehavior
{
    float timeLeft;
    float speed;
    Vec3d target;

    public HordeNudgeBehavior(Entity entity) : base(entity)
    {
        if (entity?.WatchedAttributes != null)
        {
            double x = entity.WatchedAttributes.GetDouble("hordeTargetX", 0);
            double y = entity.WatchedAttributes.GetDouble("hordeTargetY", 0);
            double z = entity.WatchedAttributes.GetDouble("hordeTargetZ", 0);
            target = new Vec3d(x, y, z);
            timeLeft = entity.WatchedAttributes.GetFloat("hordeNudgeTime", 10f);
            speed = entity.WatchedAttributes.GetFloat("hordeNudgeSpeed", 0.05f);
        }
        else
        {
            target = Vec3d.Zero;
            timeLeft = 10f;
            speed = 0.05f;
        }
    }

    public override void OnGameTick(float dt)
    {
        if (timeLeft <= 0f || entity?.ServerPos == null)
        {
            entity?.RemoveBehavior(this);
            return;
        }

        timeLeft -= dt;

        // simple steering: push motion toward target on XZ
        var here = entity.ServerPos.XYZ;
        if (target == null) return;

        var dx = target.X - here.X;
        var dz = target.Z - here.Z;
        var len = Math.Max(0.001, Math.Sqrt(dx * dx + dz * dz));

        double vx = (dx / len) * speed;
        double vz = (dz / len) * speed;

        entity.ServerPos.Motion.Add(vx, 0, vz);  // combine with existing
        entity.Pos.SetFrom(entity.ServerPos);
    }

    public override string PropertyName() => "hordenudge";
}
