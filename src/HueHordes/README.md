# 🧟 HueHordes - Enhanced Horde System for Vintage Story

[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](../../LICENSE)
[![Vintage Story](https://img.shields.io/badge/Vintage%20Story-1.21.1+-orange?style=flat-square)](https://www.vintagestory.at/)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)](https://dotnet.microsoft.com/)

> 🎯 **Advanced AI development framework with intelligent pathfinding and base targeting**

HueHordes is a sophisticated Vintage Story modification that provides advanced AI systems for entity behavior, pathfinding, and base detection. Featuring clean entity behaviors, 3D A* pathfinding, and comprehensive debug visualization tools, HueHordes serves as both a development framework and testing environment for advanced mob AI.

---

## ✨ Features

### 🤖 **Intelligent AI System**

| Feature | Description | Benefit |
|---------|-------------|---------|
| **🧭 Enhanced A* Pathfinding** | 3D pathfinding with symmetrical ±1 block movement, diagonal validation, and entity-aware coordinates | Realistic movement with proper terrain navigation |
| **🏠 Base Detection** | Automatically detects player bases and structures | Targets bases appropriately without unfair spawns |
| **🎯 Smart Targeting** | Line-of-sight detection ignoring creative/spectator players | Realistic and fair combat engagement |
| **🚪 Door Destruction** | Health-based door/gate destruction (2000HP, max 3 attackers) | Balanced base raids with proper animations |
| **✨ Visual Debug System** | Real-time particle visualization for pathfinding and base detection | Easy debugging and development tools |
| **⚡ Performance Optimized** | 5-tick update intervals with stuck detection | Smooth gameplay without lag |

### 🛠️ **Technical Features**

- **🔍 Line of Sight**: Realistic vision checks with transparent block detection
- **🎮 State Management**: Clean AI state machine (NavigatingToBase, AttackingTarget, DestroyingDoor)
- **🚶 Stuck Recovery**: Automatic detection and recovery from stuck entities
- **📊 Debug Logging**: Comprehensive logging system for troubleshooting
- **🔧 Command System**: In-game admin commands for testing and control

### 🧭 **Advanced Pathfinding System**

- **📐 Coordinate System**: Entity-aware Y=2 base level coordinates for precise movement
- **🏃 Symmetrical Movement**: ±1 block vertical movement (up via jumping, down via walking)
- **🔺 Diagonal Validation**: Corner-cutting prevention for safe diagonal movement
- **🎯 Height Validation**: 2-block entity clearance (base + body) with ground detection
- **✨ Real-time Visualization**: Particle system shows pathfinding in action
- **📍 Coordinate Logging**: Detailed debug output for path analysis

### 🎨 **Particle Debug System**

- **🌟 Base Indicators**: Colored particles for different base types
- **🛤️ Path Visualization**: Real-time path display with entity tracking
- **🎮 Console Commands**: Server-side testing without player requirements
- **📊 Debug Logging**: Coordinate tracking and placement verification
- **🔄 Dynamic Updates**: Live particle refresh and management

### 🎮 **Gameplay Features**

- **🏰 Base-Centric Combat**: Entities navigate to player bases and attack strategically
- **🚪 Realistic Sieges**: Doors require multiple hits and animations to destroy
- **👥 Multiplayer Support**: Per-player base detection and targeting
- **🧭 Smart Navigation**: Entities climb stairs, navigate terrain, and avoid obstacles naturally
- **🎯 Predictable Movement**: Symmetrical ±1 block movement creates fair and understandable AI behavior
- **⚙️ Configurable**: Adjustable debug logging and performance settings
- **🔄 Clean Integration**: Entities return to default AI after completing objectives
- **🛠️ Developer Tools**: Built-in particle debugging and pathfinding visualization

---

## 🚀 Quick Start

### 📋 **Requirements**

| Component | Version | Purpose |
|-----------|---------|---------|
| **Vintage Story** | 1.21.1+ | Base game requirement |
| **.NET 8.0 SDK** | Latest | Building from source (optional) |

### 📥 **Installation**

#### **Option 1: Download Release (Recommended)**

1. Download the latest release from [GitHub Releases](https://github.com/HueByte/VintageHue/releases)
2. Extract to your Vintage Story mods folder
3. Launch Vintage Story and verify in mod list

#### **Option 2: Build from Source**

```bash
# Clone repository
git clone https://github.com/HueByte/VintageHue.git
cd VintageHue/src/HueHordes/HueHordes

# Build mod
dotnet build

# Copy DLL to mods folder
cp bin/Debug/Mods/mod/HueHordes.dll [YOUR_MODS_FOLDER]/
```

### 📁 **Mod Installation Paths**

| Platform | Mods Directory |
|----------|----------------|
| 🪟 **Windows** | `%APPDATA%/VintageStory/Mods/` |
| 🐧 **Linux** | `~/.config/VintageStory/Mods/` |
| 🍎 **macOS** | `~/Library/Application Support/VintageStory/Mods/` |

---

## ⚙️ Configuration

### 📄 **Configuration File**

The mod automatically creates `ModConfig/Horde.server.json` with defaults:

```json
{
  "DaysBetweenHordes": 3,
  "Count": 8,
  "SpawnRadiusMin": 12.0,
  "SpawnRadiusMax": 24.0,
  "EntityCodes": ["drifter-normal"],
  "NudgeTowardInitialPos": true,
  "NudgeSeconds": 20.0,
  "NudgeSpeed": 0.05,
  "EnableDebugLogging": false,
  "DebugLoggingLevel": 1
}
```

### 🎛️ **Configuration Options**

| Setting | Type | Description |
|---------|------|-------------|
| `DaysBetweenHordes` | `int` | Days between horde events (default: 3) |
| `Count` | `int` | Number of mobs per horde (default: 8) |
| `SpawnRadiusMin/Max` | `float` | Spawn distance range (default: 12-24) |
| `EntityCodes` | `string[]` | Entity types to spawn |
| `NudgeTowardInitialPos` | `bool` | Enable movement toward player |
| `NudgeSeconds` | `float` | Duration of nudge behavior (20s) |
| `NudgeSpeed` | `float` | Movement speed multiplier (0.05) |
| `EnableDebugLogging` | `bool` | Enable detailed debug logging |
| `DebugLoggingLevel` | `int` | Logging verbosity (0-3) |

---

## 🛠️ Admin Commands

[![Admin Required](https://img.shields.io/badge/Privilege-controlserver-red?style=flat-square)](https://wiki.vintagestory.at/Commands)

### 📋 **Horde Command System**

All commands require server admin privileges:

| Command | Parameters | Description |
|---------|------------|-------------|
| `/horde spawn` | `[playername] [count] [entitytype]` | Spawn entities around player |
| `/horde detectbase` | `[playername] [radius]` | Test base detection for player |
| `/horde spawntobase` | `[playername] [count]` | Spawn entities targeting player's base |
| `/horde debug` | `[playername] [mode]` | Debug visualization with particles |

### � **Particle Debug System**

| Command | Parameters | Description |
|---------|------------|-------------|
| `/testparticles base` | `[x] [y] [z]` | Spawn base indicator particles |
| `/testparticles path` | `[x] [y] [z]` | Spawn path visualization particles |
| `/testparticles curved` | `[x] [y] [z]` | Spawn curved path particles |
| `/testparticles clear` | - | Clear all debug particles |
| `/testparticles status` | - | Show visualization system status |

### �💡 **Usage Examples**

```bash
# Basic spawning
/horde spawn                        # Spawn 3 drifters around yourself
/horde spawn PlayerName 5           # Spawn 5 entities around PlayerName
/horde spawn PlayerName 3 game:drifter # Spawn 3 drifters around PlayerName

# Base detection testing
/horde detectbase                   # Detect your base within 50 blocks
/horde detectbase PlayerName 80     # Detect PlayerName's base within 80 blocks

# Targeting tests
/horde spawntobase PlayerName 5     # Spawn 5 entities that target PlayerName's base

# Debug visualization
/horde debug PlayerName base        # Show base detection particles
/horde debug PlayerName paths       # Show pathfinding visualization

# Particle testing
/testparticles base 100 70 200      # Spawn base particles at coordinates
/testparticles path                 # Test path particles at default location
/newhorde detectbase PlayerName 75  # Detect PlayerName's base within 75 blocks

# Base targeting
/newhorde spawntobase               # Spawn 5 entities targeting your base
/newhorde spawntobase PlayerName 8  # Spawn 8 entities targeting PlayerName's base
```

### 🎯 **Entity Types**

Supported entity codes:

- `drifter-normal`, `drifter-deep`, `drifter-corrupt`
- `locust` (various types)
- Any valid Vintage Story entity code

---

## 🔧 Technical Details

### 🏗️ **Architecture Overview**

```
src/HueHordes/HueHordes/
├── 📁 AI/                        # Clean AI implementation
│   ├── 🤖 AIBehavior.cs             # Main AI behavior system
│   ├── 🗺️ AStarPathfinder.cs        # 3D pathfinding algorithm
│   ├── 🏠 BaseDetection.cs          # Player base detection
│   ├── 🚪 DoorHealthManager.cs      # Health-based door destruction
│   ├── 🎯 TargetDetection.cs        # Player targeting system
│   ├── 🏢 HordeSystem.cs            # Main system coordinator
│   └── 🌟 SpawningSystem.cs         # Entity spawning logic
├── 📊 Debug/                        # Debug logging system
├── 📋 Models/                       # Data models
└── 🎮 HueHordesModSystem.cs         # Mod integration
```

### 🧠 **AI Behavior States**

1. **NavigatingToBase**: Entity moves toward detected player base
2. **AttackingTarget**: Entity engages nearby players
3. **DestroyingDoor**: Entity attacks doors/gates with health system

### 🎯 **Pathfinding Features**

- **3D A* Algorithm**: Efficient pathfinding in 3D space
- **Obstacle Avoidance**: Smart navigation around blocks and terrain
- **Jump Mechanics**: Entities can jump over low obstacles
- **Accessibility Checks**: Finds alternative routes when direct path blocked
- **Stuck Detection**: Automatic recovery from stuck situations

### 🚪 **Door Health System**

- **2000HP per door/gate**: Balanced destruction requiring multiple hits
- **50 damage per attack**: Consistent damage with attack animations
- **3 attacker limit**: Prevents mob clustering on single door
- **Concurrent management**: Thread-safe health tracking
- **Automatic cleanup**: Removes stale door data periodically

---

## 🧪 Development

### 🛠️ **Development Setup**

```bash
# Clone and setup
git clone https://github.com/HueByte/VintageHue.git
cd VintageHue/src/HueHordes

# Build and test
cd HueHordes
dotnet build
dotnet test  # If tests are available
```

### 🔌 **Extension Points**

| Component | Extension Method | Use Case |
|-----------|------------------|----------|
| **AI Behaviors** | Extend `AIBehavior` | Custom entity behaviors |
| **Pathfinding** | Modify `AStarPathfinder` | Custom navigation logic |
| **Base Detection** | Extend `BaseDetection` | New structure types |
| **Door Systems** | Extend `DoorHealthManager` | Custom destruction mechanics |

### 🎯 **Adding Custom Features**

1. **New AI States**: Add to `AIState` enum and `AIBehavior` switch
2. **Custom Targeting**: Extend `TargetDetection` class
3. **Enhanced Base Detection**: Add new indicators to `BaseDetection`
4. **Debug Features**: Utilize `DebugLogger` for comprehensive logging

---

## 🚨 Troubleshooting

### 🔧 **Common Issues**

| Problem | Diagnosis | Solution |
|---------|-----------|----------|
| **Mod doesn't load** | Check mod manager | Verify Vintage Story 1.21.1+ compatibility |
| **Entities don't move** | AI behavior issue | Check debug logs for pathfinding errors |
| **Poor performance** | High entity count | Reduce spawn counts or increase update intervals |
| **Doors not destructible** | Health system issue | Check door type compatibility |

### 📋 **Debug Commands**

```bash
# Enable debug logging in config file
"EnableDebugLogging": true,
"DebugLoggingLevel": 2

# Test commands
/newhorde spawn           # Test basic spawning
/newhorde detectbase      # Test base detection
/newhorde spawntobase     # Test full system
```

### 📄 **Log Analysis**

Debug logs include:

- **AI Events**: State changes, pathfinding, target detection
- **Spawn Events**: Entity creation and AI behavior assignment
- **Target Events**: Player targeting and base detection
- **Door Events**: Health system and attack management

---

## 🤝 Contributing

[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](http://makeapullrequest.com)

We welcome contributions! To contribute:

1. **Fork** the repository
2. **Create** feature branch: `git checkout -b feature/amazing-feature`
3. **Implement** your changes with proper testing
4. **Follow** existing code style and patterns
5. **Submit** pull request with detailed description

### 🎯 **Contribution Areas**

- 🤖 **AI Improvements**: Enhanced behaviors and pathfinding
- 🎮 **Gameplay Features**: New mechanics and systems
- 🔧 **Performance**: Optimizations and efficiency improvements
- 📖 **Documentation**: Guides, examples, and API docs
- 🧪 **Testing**: Unit tests and integration tests

---

## 📄 License & Support

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](../../LICENSE)

### 🆘 **Getting Help**

- 📖 **Documentation**: This README and inline code comments
- 🐛 **Bug Reports**: [Open an Issue](https://github.com/HueByte/VintageHue/issues/new/choose)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/HueByte/VintageHue/discussions)
- 📧 **Direct Contact**: Create an issue for support

### 🌐 **External Resources**

- 📚 [Vintage Story Modding Wiki](https://wiki.vintagestory.at/Modding:Getting_Started)
- 🔧 [Vintage Story API Docs](https://apidocs.vintagestory.at/)
- 🎮 [Official Vintage Story Website](https://www.vintagestory.at/)

### 🏆 **Acknowledgments**

- **Anego Studios** - For the incredible Vintage Story game
- **VS Modding Community** - For tools, resources, and inspiration
- **Contributors** - Everyone who helps improve HueHordes
- **Players** - For testing, feedback, and support

---

<div align="center">

**🧟 Made with 💖 by [HueByte](https://github.com/HueByte)**

*Bringing intelligent challenge to Vintage Story survival* 🎮✨

[![Back to Repository](https://img.shields.io/badge/Back%20to-Repository-blue?style=flat-square)](../../)
[![Back to Top](https://img.shields.io/badge/Back%20to%20Top-↑-green?style=flat-square)](#-huehordes---enhanced-horde-system-for-vintage-story)

</div>
