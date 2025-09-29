using System;
using System.Collections.Generic;
using System.Linq;
using HueHordes.AI;
using HueHordes.Debug;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace HueHordes.AI;

/// <summary>
/// Clean horde system that focuses on basic functionality
/// Step 3: Integration and testing system
/// </summary>
public class HordeSystem
{
    private readonly ICoreServerAPI sapi;
    private readonly SpawningSystem spawningSystem;
    private readonly BaseDetection baseDetection;
    private readonly DoorHealthManager doorHealthManager;
    private DebugVisualization? debugVisualization;

    public HordeSystem(ICoreServerAPI serverApi)
    {
        sapi = serverApi ?? throw new ArgumentNullException(nameof(serverApi));
        doorHealthManager = new DoorHealthManager(serverApi);
        spawningSystem = new SpawningSystem(serverApi, doorHealthManager);
        baseDetection = new BaseDetection(serverApi);

        // Register cleanup timer for doors
        serverApi.Event.RegisterGameTickListener(_ => doorHealthManager.CleanupOldDoors(), 30000); // Every 30 seconds

        RegisterCommands();
    }

    /// <summary>
    /// Get the door health manager for AI behaviors
    /// </summary>
    public DoorHealthManager DoorHealthManager => doorHealthManager;

    /// <summary>
    /// Register test commands
    /// </summary>
    private void RegisterCommands()
    {
        var hordeCmd = sapi.ChatCommands
            .Create("horde")
            .RequiresPrivilege(Privilege.controlserver)
            .WithDescription("Enhanced horde system with intelligent AI");

        hordeCmd.BeginSubCommand("spawn")
            .WithDescription("Test spawn entities around player")
            .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playername"),
                     sapi.ChatCommands.Parsers.OptionalInt("count"),
                     sapi.ChatCommands.Parsers.OptionalWord("entitytype"))
            .HandleWith(OnSpawnTestCmd);

        hordeCmd.BeginSubCommand("detectbase")
            .WithDescription("Test base detection for player")
            .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playername"),
                     sapi.ChatCommands.Parsers.OptionalInt("radius"))
            .HandleWith(OnDetectBaseCmd);

        hordeCmd.BeginSubCommand("spawntobase")
            .WithDescription("Spawn entities that target player's base")
            .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playername"),
                     sapi.ChatCommands.Parsers.OptionalInt("count"))
            .HandleWith(OnSpawnToBaseCmd);

        hordeCmd.BeginSubCommand("debug")
            .WithDescription("Debug visualization - shows base detection particles and mob paths")
            .WithArgs(sapi.ChatCommands.Parsers.OptionalWord("playername"),
                     sapi.ChatCommands.Parsers.OptionalWord("mode"))
            .HandleWith(OnDebugCmd);

        DebugLogger.AIEvent("HordeSystem initialized", "Commands registered: /horde spawn, /horde detectbase, /horde spawntobase, /horde debug", "HordeSystem");
    }

    /// <summary>
    /// Test basic spawning
    /// </summary>
    private TextCommandResult OnSpawnTestCmd(TextCommandCallingArgs args)
    {
        string? playerName = (string?)args.Parsers[0].GetValue();
        int? count = (int?)args.Parsers[1].GetValue();
        string? entityType = (string?)args.Parsers[2].GetValue();

        // Determine target player
        var targetPlayer = GetTargetPlayer(playerName, args.Caller);
        if (targetPlayer == null)
        {
            return TextCommandResult.Error("Could not determine target player.");
        }

        var config = new SpawnConfig
        {
            EntityCode = entityType ?? "game:drifter",
            EntityCount = Math.Clamp(count ?? 3, 1, 10),
            MinRadius = 15f,
            MaxRadius = 40f
        };

        var spawnedEntities = spawningSystem.SpawnAroundPlayer(targetPlayer, config);

        return TextCommandResult.Success($"Spawned {spawnedEntities.Count} {config.EntityCode} around {targetPlayer.PlayerName}");
    }

    /// <summary>
    /// Test base detection
    /// </summary>
    private TextCommandResult OnDetectBaseCmd(TextCommandCallingArgs args)
    {
        string? playerName = (string?)args.Parsers[0].GetValue();
        int? radius = (int?)args.Parsers[1].GetValue();

        var targetPlayer = GetTargetPlayer(playerName, args.Caller);
        if (targetPlayer == null)
        {
            return TextCommandResult.Error("Could not determine target player.");
        }

        var searchRadius = Math.Clamp(radius ?? 50, 20, 100);
        var detectedBase = baseDetection.DetectPlayerBase(targetPlayer, searchRadius);

        if (detectedBase == null)
        {
            return TextCommandResult.Success($"No base detected for {targetPlayer.PlayerName} within {searchRadius} blocks.");
        }

        // Calculate current distance dynamically
        var currentPlayerPos = targetPlayer.Entity.ServerPos.XYZ;
        var currentDistance = currentPlayerPos.DistanceTo(detectedBase.Center);

        var result = $"Base detected for {targetPlayer.PlayerName}:\n" +
                    $"  Type: {detectedBase.BaseType}\n" +
                    $"  Score: {detectedBase.BaseScore}\n" +
                    $"  Center: {detectedBase.Center.X:F1}, {detectedBase.Center.Y:F1}, {detectedBase.Center.Z:F1}\n" +
                    $"  Distance from player: {currentDistance:F1} blocks\n" +
                    $"  Indicators found: {detectedBase.IndicatorCount}\n" +
                    $"  Player position: {currentPlayerPos.X:F1}, {currentPlayerPos.Y:F1}, {currentPlayerPos.Z:F1}\n";

        if (detectedBase.BedPosition != null)
        {
            result += $"  Bed position: {detectedBase.BedPosition.X:F1}, {detectedBase.BedPosition.Y:F1}, {detectedBase.BedPosition.Z:F1}\n";
        }

        return TextCommandResult.Success(result);
    }

    /// <summary>
    /// Test spawning entities that target base
    /// </summary>
    private TextCommandResult OnSpawnToBaseCmd(TextCommandCallingArgs args)
    {
        string? playerName = (string?)args.Parsers[0].GetValue();
        int? count = (int?)args.Parsers[1].GetValue();

        var targetPlayer = GetTargetPlayer(playerName, args.Caller);
        if (targetPlayer == null)
        {
            return TextCommandResult.Error("Could not determine target player.");
        }

        // First detect the base
        var detectedBase = baseDetection.DetectPlayerBase(targetPlayer, 60);
        if (detectedBase == null)
        {
            return TextCommandResult.Error($"No base detected for {targetPlayer.PlayerName}. Cannot spawn targeting entities.");
        }

        // Spawn entities around the player
        var config = new SpawnConfig
        {
            EntityCode = "game:drifter",
            EntityCount = Math.Clamp(count ?? 5, 1, 10),
            MinRadius = 20f,
            MaxRadius = 50f
        };

        var spawnedEntities = spawningSystem.SpawnAroundPlayer(targetPlayer, config);

        // Set base as target for all spawned entities
        foreach (var entity in spawnedEntities)
        {
            entity.WatchedAttributes.SetDouble("baseTargetX", detectedBase.Center.X);
            entity.WatchedAttributes.SetDouble("baseTargetY", detectedBase.Center.Y);
            entity.WatchedAttributes.SetDouble("baseTargetZ", detectedBase.Center.Z);
            entity.WatchedAttributes.SetString("baseType", detectedBase.BaseType);

            DebugLogger.AITarget(
                entity.EntityId.ToString(),
                "Base",
                $"{detectedBase.BaseType}@{detectedBase.Center.X:F1},{detectedBase.Center.Y:F1},{detectedBase.Center.Z:F1}",
                "Base detection targeting"
            );
        }

        return TextCommandResult.Success($"Spawned {spawnedEntities.Count} entities targeting {detectedBase.BaseType} " +
                                       $"at {detectedBase.Center.X:F1},{detectedBase.Center.Y:F1},{detectedBase.Center.Z:F1}");
    }

    /// <summary>
    /// Get target player from name or caller
    /// </summary>
    private IServerPlayer? GetTargetPlayer(string? playerName, Caller? caller)
    {
        if (!string.IsNullOrEmpty(playerName))
        {
            return sapi.World.AllOnlinePlayers
                .FirstOrDefault(p => p?.PlayerName?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true) as IServerPlayer;
        }

        return caller?.Player as IServerPlayer;
    }

    /// <summary>
    /// Debug visualization command
    /// </summary>
    private TextCommandResult OnDebugCmd(TextCommandCallingArgs args)
    {
        string? playerName = (string?)args.Parsers[0].GetValue();
        string? mode = (string?)args.Parsers[1].GetValue();

        var targetPlayer = GetTargetPlayer(playerName, args.Caller);
        if (targetPlayer == null)
        {
            return TextCommandResult.Error("Could not determine target player.");
        }

        // Initialize debug visualization if not already done
        if (debugVisualization == null)
        {
            debugVisualization = new DebugVisualization(sapi);
        }

        switch (mode?.ToLower())
        {
            case "base":
            case null: // Default to base visualization
                return VisualizeBase(targetPlayer);

            case "paths":
                return VisualizePaths(targetPlayer);

            case "clear":
                debugVisualization.ClearAllParticles();
                return TextCommandResult.Success("Debug particles cleared.");

            default:
                return TextCommandResult.Error("Valid modes: base, paths, clear");
        }
    }

    private TextCommandResult VisualizeBase(IServerPlayer player)
    {
        var detectedBase = baseDetection.DetectPlayerBase(player, 60);
        if (detectedBase == null)
        {
            return TextCommandResult.Error($"No base detected for {player.PlayerName}.");
        }

        // Clear previous base particles
        debugVisualization.ClearBaseParticles();

        // Get base indicators for visualization
        var indicators = GetBaseIndicatorsForVisualization(player.Entity.ServerPos.XYZ, 60);

        foreach (var indicator in indicators)
        {
            debugVisualization.ShowBaseIndicator(indicator);
        }

        return TextCommandResult.Success($"Visualized {indicators.Count} base indicators for {player.PlayerName}. " +
                                       $"Base center: {detectedBase.Center.X:F1}, {detectedBase.Center.Y:F1}, {detectedBase.Center.Z:F1}");
    }

    private TextCommandResult VisualizePaths(IServerPlayer player)
    {
        // This will be called by AI entities to show their paths
        debugVisualization.EnablePathVisualization(true);

        return TextCommandResult.Success($"Path visualization enabled. Mobs will now show their pathfinding particles.");
    }

    /// <summary>
    /// Get base indicators for visualization - mirror of BaseDetection logic
    /// </summary>
    private List<BaseIndicator> GetBaseIndicatorsForVisualization(Vec3d playerPos, int radius)
    {
        var indicators = new List<BaseIndicator>();

        var startX = (int)(playerPos.X - radius);
        var endX = (int)(playerPos.X + radius);
        var startZ = (int)(playerPos.Z - radius);
        var endZ = (int)(playerPos.Z + radius);

        var minY = Math.Max(0, (int)(playerPos.Y - 20));
        var maxY = Math.Min(255, (int)(playerPos.Y + 20));

        // Scan the area for base indicators - same logic as BaseDetection
        for (int x = startX; x <= endX; x += 4)
        {
            for (int z = startZ; z <= endZ; z += 4)
            {
                for (int y = minY; y <= maxY; y += 2)
                {
                    var blockPos = new BlockPos(x, y, z);
                    if (!sapi.World.BlockAccessor.IsValidPos(blockPos))
                        continue;

                    var block = sapi.World.BlockAccessor.GetBlock(blockPos);
                    if (block.Id == 0) continue;

                    var indicator = ClassifyBlockForVisualization(block, blockPos.ToVec3d());
                    if (indicator != null)
                    {
                        indicators.Add(indicator);
                    }
                }
            }
        }

        return indicators;
    }

    /// <summary>
    /// Classify blocks for visualization - mirror of BaseDetection logic
    /// </summary>
    private BaseIndicator? ClassifyBlockForVisualization(Block block, Vec3d position)
    {
        var blockCode = block.Code?.ToString();
        if (blockCode == null) return null;

        // Same classification logic as BaseDetection
        if (blockCode.Contains("door", StringComparison.OrdinalIgnoreCase))
        {
            return new BaseIndicator
            {
                Position = position,
                Type = BaseIndicatorType.Door,
                Priority = 10,
                Range = 15f
            };
        }

        if (blockCode.Contains("bed", StringComparison.OrdinalIgnoreCase))
        {
            return new BaseIndicator
            {
                Position = position,
                Type = BaseIndicatorType.Bed,
                Priority = 10,
                Range = 20f
            };
        }

        if (blockCode.Contains("chest", StringComparison.OrdinalIgnoreCase))
        {
            return new BaseIndicator
            {
                Position = position,
                Type = BaseIndicatorType.Storage,
                Priority = 6,
                Range = 12f
            };
        }

        if (blockCode.Contains("workbench", StringComparison.OrdinalIgnoreCase) ||
            blockCode.Contains("anvil", StringComparison.OrdinalIgnoreCase) ||
            blockCode.Contains("forge", StringComparison.OrdinalIgnoreCase) ||
            blockCode.Contains("firepit", StringComparison.OrdinalIgnoreCase))
        {
            return new BaseIndicator
            {
                Position = position,
                Type = BaseIndicatorType.Crafting,
                Priority = 5,
                Range = 10f
            };
        }

        if (blockCode.Contains("bricks", StringComparison.OrdinalIgnoreCase) ||
            blockCode.Contains("planks", StringComparison.OrdinalIgnoreCase) ||
            (blockCode.Contains("stone", StringComparison.OrdinalIgnoreCase) && !blockCode.Contains("cobble", StringComparison.OrdinalIgnoreCase)))
        {
            return new BaseIndicator
            {
                Position = position,
                Type = BaseIndicatorType.Construction,
                Priority = 2,
                Range = 8f
            };
        }

        return null;
    }

    /// <summary>
    /// Get base detection system for external access
    /// </summary>
    public BaseDetection GetBaseDetection() => baseDetection;

    /// <summary>
    /// Get spawning system for external access
    /// </summary>
    public SpawningSystem GetSpawningSystem() => spawningSystem;

    /// <summary>
    /// Get debug visualization system
    /// </summary>
    public DebugVisualization? GetDebugVisualization() => debugVisualization;
}
