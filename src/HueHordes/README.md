# 🧟 HueHordes - Advanced Horde System for Vintage Story

[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](../../LICENSE)
[![Build Status](https://img.shields.io/github/actions/workflow/status/HueByte/VintageHue/mod-tests.yml?branch=master&style=flat-square&label=Build)](https://github.com/HueByte/VintageHue/actions/workflows/mod-tests.yml)
[![Documentation](https://img.shields.io/badge/Docs-GitHub%20Pages-brightgreen?style=flat-square)](https://huebyte.github.io/VintageHue/HueHordes/)
[![Vintage Story](https://img.shields.io/badge/Vintage%20Story-1.21.1+-orange?style=flat-square)](https://www.vintagestory.at/)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)](https://dotnet.microsoft.com/)

> 🎯 **Transform your Vintage Story survival experience with intelligent, dynamic horde spawning that adapts to your playstyle**

HueHordes is a cutting-edge Vintage Story modification that revolutionizes survival gameplay through an advanced horde spawning system. Unlike traditional spawning systems, HueHordes features sophisticated AI that intelligently detects player bases and spawns challenging waves of enemies that enhance rather than frustrate your gameplay experience.

---

## ✨ Features

### 🎯 **Intelligent Horde System**

| Feature | Description | Benefit |
|---------|-------------|---------|
| 🤖 **Smart Base Detection** | Automatically detects player structures, walls, and enclosed areas | Spawns enemies outside your base, not inside it |
| ⚡ **Async AI Architecture** | Modern .NET 8 task-based programming with high performance | Smooth gameplay without lag or stuttering |
| 🎯 **Dynamic Targeting** | Advanced priority-based target selection and switching | Intelligent enemy behavior that feels natural |
| 🏠 **Bed-Centered Detection** | Uses player beds as focal points for base detection | Accurate base boundaries and patrol areas |
| 👁️ **Line-of-Sight AI** | Entities adapt when losing visual contact with targets | Realistic enemy behavior and engagement |
| 🚶 **Patrol Behavior** | Enemies patrol around detected bases when targets are lost | Persistent threat without overwhelming players |

### ⚙️ **Configuration & Control**

- 📅 **Flexible Timing**: Configurable spawn intervals (default: every 3 in-game days)
- 🔢 **Customizable Spawns**: Adjust entity counts, types, and spawn radius
- 👥 **Per-Player Tracking**: Independent horde timers for each player
- 🛠️ **Admin Commands**: Complete control over horde timing and testing
- 🔄 **Hot Reload**: Configuration updates without server restart
- 📊 **Performance Monitoring**: Built-in metrics and statistics

### 🛡️ **Reliability & Performance**

- 🧪 **Thoroughly Tested**: 33+ unit and integration tests with 100% pass rate
- 🔧 **Graceful Degradation**: Fallback systems ensure stable operation
- 📈 **Performance Optimized**: Concurrent processing with semaphores and channels
- 🔍 **Comprehensive Logging**: Detailed logging for troubleshooting
- 🚫 **Zero Dependencies**: No external mod requirements

---

## 🚀 Quick Start

### 📋 **Requirements**

[![Vintage Story](https://img.shields.io/badge/Vintage%20Story-1.21.1+-orange?style=flat-square)](https://www.vintagestory.at/)
[![.NET SDK](https://img.shields.io/badge/.NET%20SDK-8.0+-purple?style=flat-square)](https://dotnet.microsoft.com/)

| Component | Version | Purpose |
|-----------|---------|---------|
| **Vintage Story** | 1.21.1+ | Base game requirement |
| **.NET 8.0 SDK** | Latest | Building from source (optional) |
| **VINTAGE_STORY** | Environment Variable | Build system integration |

### 📥 **Installation Options**

#### **Option 1: Download Release (Recommended)**

[![GitHub Release](https://img.shields.io/github/v/release/HueByte/VintageHue?style=flat-square&label=Latest%20Release)](https://github.com/HueByte/VintageHue/releases)

1. Download the latest `huehordes_*.zip` from [Releases](https://github.com/HueByte/VintageHue/releases)
2. Extract to your Vintage Story mods folder
3. Launch Vintage Story and verify in mod list

#### **Option 2: Build from Source**

```bash
# Clone repository
git clone https://github.com/HueByte/VintageHue.git
cd VintageHue/src/HueHordes

# Set environment variable (Windows)
setx VINTAGE_STORY "C:\Path\To\VintageStory"

# Set environment variable (Linux/macOS)
export VINTAGE_STORY="/path/to/vintagestory"

# Build mod
./build.ps1      # Windows
./build.sh       # Linux/macOS
```

### 📁 **Mod Installation Paths**

| Platform | Mods Directory |
|----------|----------------|
| 🪟 **Windows** | `%APPDATA%/VintageStory/Mods/` |
| 🐧 **Linux** | `~/.config/VintageStory/Mods/` |
| 🍎 **macOS** | `~/Library/Application Support/VintageStory/Mods/` |

### ✅ **Verification**

After installation:

1. Launch Vintage Story
2. Go to **Main Menu → Mod Manager**
3. Verify "**HueHordes**" appears in the mod list
4. Look for the ✅ checkmark indicating successful load

---

## ⚙️ Configuration

### 📄 **Configuration File**

The mod automatically creates `ModConfig/Horde.server.json` with intelligent defaults:

```json
{
  "DaysBetweenHordes": 3,
  "Count": 8,
  "SpawnRadiusMin": 12.0,
  "SpawnRadiusMax": 24.0,
  "EntityCodes": ["drifter-normal"],
  "NudgeTowardInitialPos": true,
  "NudgeSeconds": 20.0,
  "NudgeSpeed": 0.05
}
```

### 🎛️ **Configuration Reference**

| Setting | Type | Range | Description |
|---------|------|-------|-------------|
| `DaysBetweenHordes` | `int` | 1-30 | In-game days between horde events |
| `Count` | `int` | 1-50 | Number of entities per horde |
| `SpawnRadiusMin` | `float` | 5.0-50.0 | Minimum spawn distance from player |
| `SpawnRadiusMax` | `float` | 10.0-100.0 | Maximum spawn distance from player |
| `EntityCodes` | `string[]` | Valid entities | Array of entity types to spawn |
| `NudgeTowardInitialPos` | `bool` | true/false | Enable initial movement toward player |
| `NudgeSeconds` | `float` | 5.0-60.0 | Duration of nudge behavior |
| `NudgeSpeed` | `float` | 0.01-1.0 | Movement speed multiplier |

### 🎯 **Popular Entity Types**

```json
"EntityCodes": [
  "drifter-normal",      // Standard drifters
  "drifter-corrupt",     // Corrupted drifters
  "locust-basic",        // Basic locusts
  "wolf-male",           // Male wolves
  "wolf-female",         // Female wolves
  "hyena-male",          // Male hyenas
  "bear-black"           // Black bears
]
```

### 🔄 **Hot Reload**

Configuration changes are automatically detected and applied without server restart. Simply edit the JSON file and save!

---

## 🛠️ Admin Commands

[![Admin Required](https://img.shields.io/badge/Privilege-controlserver-red?style=flat-square)](https://wiki.vintagestory.at/Commands)

Server administrators can control horde behavior with comprehensive commands:

### 📋 **Command Reference**

| Command | Parameters | Description |
|---------|------------|-------------|
| `/horde now` | - | Trigger immediate horde for calling player |
| `/horde reset` | - | Clear all player horde timers |
| `/horde setdays` | `<number>` | Set days between hordes (1-30) |
| `/horde status` | - | Show configuration and player timer status |
| `/horde spawn` | `<playername>` | Spawn horde for specific player |
| `/horde aiinfo` | - | Show AI system and base detection info |
| `/horde refreshbase` | `<playername>` | Force refresh base detection |

### 💡 **Usage Examples**

```bash
# Testing & Development
/horde now                    # Test horde spawn for yourself
/horde spawn PlayerName       # Test for specific player
/horde aiinfo                 # Debug AI system status

# Server Management
/horde reset                  # Reset all timers after changes
/horde setdays 5              # Adjust spawn frequency
/horde status                 # Monitor system health

# Base Detection
/horde refreshbase PlayerName # Update base detection
```

### 🆘 **Integrated Help System**

Get detailed help for any command:

```bash
/help horde                   # List all horde commands
/help horde setdays           # Specific command help
```

### ✨ **Advanced Features**

- **🔍 Smart Validation**: Automatic argument validation and error messages
- **📊 Detailed Status**: Comprehensive system and player information
- **🎯 Player-Specific**: Target specific players for testing
- **🔧 Real-Time Updates**: Changes apply immediately

---

## 🔧 Development

### 🏗️ **Architecture Overview**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple?style=flat-square)](https://dotnet.microsoft.com/)
[![Async/Await](https://img.shields.io/badge/Pattern-Async%2FAwait-blue?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/)
[![Tests](https://img.shields.io/badge/Tests-33%2B-green?style=flat-square)](HueHordes.Test/)

```
src/HueHordes/
├── 📁 HueHordes/                    # Main mod project
│   ├── 🎯 HueHordesModSystem.cs     # Core mod implementation
│   ├── 📁 Models/                   # Data models & state
│   │   ├── ServerConfig.cs          # Configuration model
│   │   ├── HordeSaveData.cs         # Save data structure
│   │   └── HordeState.cs            # Per-player state
│   ├── 📋 modinfo.json              # Mod metadata
│   └── 📄 HueHordes.csproj          # Project configuration
├── 📁 HueHordes.Test/               # Comprehensive test suite
├── 📁 CakeBuild/                    # Build automation
│   ├── Program.cs                   # Build tasks & logic
│   └── CakeBuild.csproj             # Build project
└── 📄 Main.sln                      # Solution file
```

### 🛠️ **Development Setup**

#### **Prerequisites**

```bash
# Required tools
dotnet --version    # .NET 8.0+
git --version       # Git for version control
```

#### **Quick Development Start**

```bash
# Clone and setup
git clone https://github.com/HueByte/VintageHue.git
cd VintageHue/src/HueHordes

# Environment setup
setx VINTAGE_STORY "C:\Path\To\VintageStory"  # Windows
export VINTAGE_STORY="/path/to/vintagestory"  # Linux/macOS

# Build and test
./build.ps1          # Full build with tests
dotnet test          # Run test suite only
```

### 🧪 **Testing Framework**

[![xUnit](https://img.shields.io/badge/Framework-xUnit-blue?style=flat-square)](https://xunit.net/)
[![FluentAssertions](https://img.shields.io/badge/Assertions-Fluent-orange?style=flat-square)](https://fluentassertions.com/)
[![Moq](https://img.shields.io/badge/Mocking-Moq-red?style=flat-square)](https://github.com/moq/moq4)

- **33+ Tests**: Comprehensive unit and integration testing
- **100% Pass Rate**: All tests consistently pass
- **Performance Tests**: Verify async performance and memory usage
- **Mock Integration**: Isolated testing with Vintage Story API mocks

### 🔌 **Extension Points**

| Component | Extension Method | Use Case |
|-----------|------------------|----------|
| **Entity Behaviors** | Add `EntityBehavior` classes | Custom AI behaviors |
| **Spawn Logic** | Modify `SpawnHordeFor()` | Custom spawn algorithms |
| **Base Detection** | Extend detection algorithms | New structure types |
| **Commands** | Add command handlers | Additional admin tools |
| **Configuration** | Extend `ServerConfig` | New settings |

### 🎯 **Adding Custom Entities**

1. **Find Entity Codes**: Check Vintage Story assets or mod files
2. **Update Configuration**: Add to `EntityCodes` array
3. **Test Spawn**: Use `/horde now` to verify
4. **Balance Gameplay**: Adjust counts and timing

```json
"EntityCodes": [
  "your-custom-entity",
  "modded-creature-id"
]
```

---

## 🚨 Troubleshooting

### 🔧 **Build Issues**

| Issue | Solution |
|-------|----------|
| **VintagestoryAPI.dll not found** | Set `VINTAGE_STORY` environment variable to game installation path |
| **JSON validation failed** | Check JSON syntax; use `--skipJsonValidation` flag to bypass |
| **Build script permission denied** | Run `chmod +x build.sh` on Linux/macOS |
| **.NET SDK not found** | Install .NET 8.0 SDK from [Microsoft](https://dotnet.microsoft.com/) |

### 🎮 **Runtime Issues**

| Problem | Diagnosis | Solution |
|---------|-----------|----------|
| **Mod doesn't load** | Check mod manager for errors | Verify game version compatibility (1.21.1+) |
| **Hordes not spawning** | Configuration or timer issue | Use `/horde status` and `/horde now` to test |
| **Entities don't move** | AI behavior disabled | Check `NudgeTowardInitialPos: true` in config |
| **Performance issues** | High entity count or complex AI | Reduce `Count` setting or adjust spawn radius |
| **Base detection fails** | Missing bed or complex structure | Use `/horde refreshbase PlayerName` to update |

### 📋 **Debug Commands**

```bash
/horde status                 # Check system health
/horde aiinfo                 # Debug AI and base detection
/horde now                    # Test immediate spawn
```

### 📄 **Log Analysis**

Check Vintage Story logs at:

- **Windows**: `%APPDATA%/VintageStory/Logs/`
- **Linux**: `~/.config/VintageStory/Logs/`
- **macOS**: `~/Library/Application Support/VintageStory/Logs/`

---

## 🤝 Contributing

[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](http://makeapullrequest.com)
[![Contributor Covenant](https://img.shields.io/badge/Contributor%20Covenant-2.1-4baaaa.svg?style=flat-square)](../../CODE_OF_CONDUCT.md)

We welcome contributions! Please read our [Contributing Guide](../../CONTRIBUTING.md) for details on:

- 🛠️ **Development workflow**
- 📝 **Code standards**
- 🧪 **Testing requirements**
- 📋 **Pull request process**

### 🎯 **Quick Contribution Steps**

1. **Fork** the repository
2. **Create** feature branch: `git checkout -b feature/amazing-feature`
3. **Write** tests for new functionality
4. **Implement** your changes
5. **Test** thoroughly: `dotnet test && ./build.ps1`
6. **Submit** pull request with detailed description

---

## 📄 License & Support

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](../../LICENSE)

### 🆘 **Getting Help**

- 📖 **Documentation**: [GitHub Pages](https://huebyte.github.io/VintageHue/HueHordes/)
- 🐛 **Bug Reports**: [Open an Issue](https://github.com/HueByte/VintageHue/issues/new/choose)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/HueByte/VintageHue/discussions)
- 📧 **Direct Contact**: Create an issue for direct support

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
[![Back to Top](https://img.shields.io/badge/Back%20to%20Top-↑-green?style=flat-square)](#-huehordes---advanced-horde-system-for-vintage-story)

</div>
