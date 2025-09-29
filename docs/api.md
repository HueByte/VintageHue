# 📚 HueHordes API Reference

> Comprehensive API documentation for the HueHordes Vintage Story mod

This document provides detailed information about the HueHordes mod architecture, classes, and extension points for developers who want to understand or extend the mod.

---

## 🏗️ Architecture Overview

HueHordes follows a clean, modular architecture with separated concerns:

```
HueHordes/
├── 🎮 HueHordesModSystem.cs      # Main mod entry point
├── 📁 AI/                     # Core AI implementation
│   ├── 🤖 AIBehavior.cs          # Entity AI behavior
│   ├── 🗺️ AStarPathfinder.cs     # 3D pathfinding
│   ├── 🏠 BaseDetection.cs       # Player base detection
│   ├── 🚪 DoorHealthManager.cs   # Door destruction system
│   ├── 🎯 TargetDetection.cs     # Player targeting
│   ├── 🏢 HordeSystem.cs         # Main coordinator
│   └── 🌟 SpawningSystem.cs      # Entity spawning
├── 📊 Debug/                     # Logging system
│   └── 🔍 DebugLogger.cs         # Centralized logging
└── 📋 Models/                    # Data models
    └── ⚙️ ServerConfig.cs        # Configuration
```

---

## 🎮 Core Classes

### HueHordesModSystem

**Namespace**: `HueHordes`
**Purpose**: Main mod system that integrates all components

```csharp
public class HueHordesModSystem : ModSystem
{
    public override void StartServerSide(ICoreServerAPI api)
    public override void Dispose()
}
```

**Key Responsibilities**:

- Mod initialization and cleanup
- Configuration loading and management
- Integration with HordeSystem

---

## 🤖 AI System Classes

### AIBehavior

**Namespace**: `HueHordes.AI`
**Purpose**: Core AI behavior for spawned entities

```csharp
public class AIBehavior : EntityBehavior
{
    public AIBehavior(Entity entity, DoorHealthManager doorHealthManager)
    public override void OnGameTick(float dt)
    public override string PropertyName() => "ai"
}
```

**Key Features**:

- State machine with 3 states: `NavigatingToBase`, `AttackingTarget`, `DestroyingDoor`
- Performance optimized with 5-tick update intervals
- Automatic stuck detection and recovery
- Clean integration with Vintage Story's entity system

**AI States**:

```csharp
public enum AIState
{
    NavigatingToBase,    // Moving toward player base
    AttackingTarget,     // Engaging nearby players
    DestroyingDoor      // Attacking doors/gates
}
```

### AStarPathfinder

**Namespace**: `HueHordes.AI`
**Purpose**: 3D pathfinding algorithm for entity navigation

```csharp
public class AStarPathfinder
{
    public AStarPathfinder(ICoreServerAPI sapi)
    public List<Vec3d> FindPath(Vec3d start, Vec3d end, int maxNodes = 10000)
    public Vec3d? FindNearestAccessiblePosition(Vec3d targetPos, int searchRadius = 5)
}
```

**Key Features**:

- 3D A* pathfinding algorithm
- Obstacle avoidance and jump mechanics
- Accessibility checking for unreachable targets
- Performance optimized with node limits

**Supporting Classes**:

```csharp
public class PathNode : IComparable<PathNode>
{
    public Vec3d Position { get; set; }
    public float GCost { get; set; }  // Distance from start
    public float HCost { get; set; }  // Distance to end
    public float FCost => GCost + HCost;
    public PathNode? Parent { get; set; }
}

public class PriorityQueue<T> where T : IComparable<T>
{
    public void Enqueue(T item)
    public T Dequeue()
    public int Count { get; }
}
```

### BaseDetection

**Namespace**: `HueHordes.AI`
**Purpose**: Intelligent detection of player bases and structures

```csharp
public class BaseDetection
{
    public BaseDetection(ICoreServerAPI serverApi)
    public DetectedBase? DetectPlayerBase(IServerPlayer player, int searchRadius)
    public Vec3d? FindPlayerBed(IServerPlayer player, int searchRadius = 50)
}
```

**Detection Algorithm**:

- Scans for key indicators: beds, doors, chests, workstations
- Calculates base score based on structure density
- Classifies base types: `HomeBase`, `WorkshopBase`, `BasicBase`, `Outpost`
- Caches results for performance

**DetectedBase Model**:

```csharp
public class DetectedBase
{
    public Vec3d Center { get; set; }
    public string BaseType { get; set; }
    public int BaseScore { get; set; }
    public int IndicatorCount { get; set; }
    public double DistanceFromPlayer { get; set; }
    public Vec3d? BedPosition { get; set; }
}
```

### DoorHealthManager

**Namespace**: `HueHordes.AI`
**Purpose**: Health-based door destruction with attacker limits

```csharp
public class DoorHealthManager
{
    public DoorHealthManager(ICoreServerAPI serverApi)
    public bool TryRegisterAttacker(Vec3d doorPos, long entityId)
    public void UnregisterAttacker(Vec3d doorPos, long entityId)
    public bool AttackDoor(Vec3d doorPos, long entityId, float damage = 50f)
    public void CleanupOldDoors()
}
```

**Features**:

- 2000HP per door/gate
- Maximum 3 concurrent attackers per door
- Automatic cleanup of destroyed doors
- Thread-safe attacker management

**DoorInfo Model**:

```csharp
public class DoorInfo
{
    public Vec3i Position { get; set; }
    public float MaxHealth { get; set; } = 2000f;
    public float CurrentHealth { get; set; } = 2000f;
    public int MaxAttackers { get; set; } = 3;
    public HashSet<long> CurrentAttackers { get; set; }
    public long LastAttackTime { get; set; }
    public float HealthPercentage => CurrentHealth / MaxHealth;
}
```

### TargetDetection

**Namespace**: `HueHordes.AI`
**Purpose**: Player targeting with line-of-sight and game mode filtering

```csharp
public class TargetDetection
{
    public TargetDetection(ICoreServerAPI serverApi)
    public Entity? FindNearestPlayer(Vec3d position, float maxRange)
    public Entity[] FindAllPlayersInRange(Vec3d position, float maxRange)
    public bool IsValidTarget(Entity entity)
    public Vec3d GetTargetAimPoint(Entity target)
    public Vec3d PredictTargetPosition(Entity target, float predictionTime)
}
```

**Smart Features**:

- Ignores creative and spectator mode players
- Line-of-sight checking with transparent block support
- Target prediction for moving players
- Realistic aim point calculation

---

## 🏢 System Coordination

### HordeSystem

**Namespace**: `HueHordes.AI`
**Purpose**: Main coordinator for all AI systems

```csharp
public class HordeSystem
{
    public HordeSystem(ICoreServerAPI serverApi)
    public DoorHealthManager DoorHealthManager { get; }
    public BaseDetection GetBaseDetection()
    public SpawningSystem GetSpawningSystem()
}
```

**Command System**:

- `/newhorde spawn [playername] [count] [entitytype]`
- `/newhorde detectbase [playername] [radius]`
- `/newhorde spawntobase [playername] [count]`

### SpawningSystem

**Namespace**: `HueHordes.AI`
**Purpose**: Entity spawning with AI behavior assignment

```csharp
public class SpawningSystem
{
    public SpawningSystem(ICoreServerAPI serverApi, DoorHealthManager doorHealthManager)
    public List<Entity> SpawnAroundPlayer(IServerPlayer player, SpawnConfig config)
}
```

**SpawnConfig Model**:

```csharp
public class SpawnConfig
{
    public string EntityCode { get; set; } = "game:drifter-normal";
    public int EntityCount { get; set; } = 5;
    public float MinRadius { get; set; } = 20f;
    public float MaxRadius { get; set; } = 50f;

    public static SpawnConfig Default { get; }
}
```

---

## 📊 Debug System

### DebugLogger

**Namespace**: `HueHordes.Debug`
**Purpose**: Centralized logging system for debugging and monitoring

```csharp
public static class DebugLogger
{
    public static void Initialize(ICoreServerAPI api, bool enableDebugLogging, int debugLevel)
    public static void AIEvent(string eventName, string details, string entityId = "")
    public static void AISpawn(string entityType, string position, string target)
    public static void AITarget(string entityId, string targetType, string targetName, string context)
    public static void Event(string eventName, string details)
    public static void Error(string message, Exception? ex = null)
}
```

**Logging Categories**:

- **AI Events**: State changes, pathfinding, behavior
- **Spawn Events**: Entity creation and placement
- **Target Events**: Player targeting and detection
- **General Events**: System operations
- **Errors**: Exception handling and troubleshooting

---

## 📋 Configuration

### ServerConfig

**Namespace**: `HueHordes.Models`
**Purpose**: Mod configuration with hot-reload support

```csharp
public class ServerConfig
{
    public bool EnableDebugLogging { get; set; } = false;
    public int DebugLoggingLevel { get; set; } = 1;
}
```

**Configuration File**: `ModConfig/Horde.server.json`

---

## 🔌 Extension Points

### Creating Custom AI Behaviors

Extend the `AIBehavior` class to create custom entity behaviors:

```csharp
public class CustomAIBehavior : AIBehavior
{
    public CustomAIBehavior(Entity entity, DoorHealthManager doorHealthManager)
        : base(entity, doorHealthManager)
    {
        // Custom initialization
    }

    protected override void HandleCustomState(Vec3d entityPos, long currentTime)
    {
        // Custom behavior logic
    }
}
```

### Adding New AI States

1. Extend the `AIState` enum:

```csharp
public enum AIState
{
    NavigatingToBase,
    AttackingTarget,
    DestroyingDoor,
    CustomState  // Your new state
}
```

2. Add handling in `AIBehavior.OnGameTick()`:

```csharp
switch (currentState)
{
    case AIState.CustomState:
        HandleCustomState(entityPos, currentTime);
        break;
}
```

### Custom Base Detection

Extend `BaseDetection` to add new structure types:

```csharp
public class CustomBaseDetection : BaseDetection
{
    protected override bool IsCustomIndicator(Block block)
    {
        // Custom structure detection logic
        return block.Code?.ToString().Contains("custom-structure") == true;
    }
}
```

### Enhanced Door Systems

Extend `DoorHealthManager` for custom destruction mechanics:

```csharp
public class CustomDoorHealthManager : DoorHealthManager
{
    public bool AttackCustomStructure(Vec3d pos, long entityId, float damage)
    {
        // Custom destruction logic
        return base.AttackDoor(pos, entityId, damage);
    }
}
```

---

## 🧪 Testing and Development

### Debug Configuration

Enable comprehensive logging:

```json
{
  "EnableDebugLogging": true,
  "DebugLoggingLevel": 3
}
```

### Testing Commands

Use admin commands for development testing:

```bash
# Test basic functionality
/newhorde spawn TestPlayer 1 drifter-normal

# Test base detection
/newhorde detectbase TestPlayer 50

# Test full system integration
/newhorde spawntobase TestPlayer 3
```

### Performance Monitoring

Monitor performance through debug logs:

- AI update frequency (5-tick intervals)
- Pathfinding performance (node counts, timing)
- Memory usage (entity cleanup, path caching)
- Concurrent attacker limits (door health system)

---

## 📄 API Usage Examples

### Basic Entity Spawning

```csharp
var spawningSystem = hordeSystem.GetSpawningSystem();
var config = new SpawnConfig
{
    EntityCode = "game:drifter-normal",
    EntityCount = 5,
    MinRadius = 20f,
    MaxRadius = 40f
};

var entities = spawningSystem.SpawnAroundPlayer(player, config);
```

### Base Detection

```csharp
var baseDetection = hordeSystem.GetBaseDetection();
var detectedBase = baseDetection.DetectPlayerBase(player, 60);

if (detectedBase != null)
{
    Console.WriteLine($"Found {detectedBase.BaseType} at {detectedBase.Center}");
}
```

### Door Health Management

```csharp
var doorManager = hordeSystem.DoorHealthManager;
if (doorManager.TryRegisterAttacker(doorPosition, entityId))
{
    bool destroyed = doorManager.AttackDoor(doorPosition, entityId, 50f);
    if (destroyed)
    {
        // Door was destroyed
    }
}
```

---

<div align="center">

**📚 API Reference Complete**

For more information, see the [main documentation](../README.md) or [contributing guide](../CONTRIBUTING.md).

[![Back to Documentation](https://img.shields.io/badge/Back%20to-Documentation-blue?style=flat-square)](../README.md)

</div>
