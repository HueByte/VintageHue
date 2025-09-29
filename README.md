# 🎮 VintageHue - Vintage Story Mod Collection

[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![GitHub Release](https://img.shields.io/github/v/release/HueByte/VintageHue?style=flat-square)](https://github.com/HueByte/VintageHue/releases)
[![GitHub Stars](https://img.shields.io/github/stars/HueByte/VintageHue?style=flat-square)](https://github.com/HueByte/VintageHue/stargazers)
[![Issues](https://img.shields.io/github/issues/HueByte/VintageHue?style=flat-square)](https://github.com/HueByte/VintageHue/issues)

> A curated collection of high-quality mods for Vintage Story, crafted with passion by **HueByte** 🦄

Welcome to **VintageHue** - my personal collection of Vintage Story modifications! This repository houses multiple mods that enhance the Vintage Story experience through innovative gameplay mechanics, quality-of-life improvements, and exciting new content.

## Repository Structure

This repository follows a modular structure where each mod is contained in its own dedicated folder within the `src` directory:

```
VintageHue/
├── README.md                 # This file - repository overview
├── LICENSE                   # Repository license
├── .gitignore               # Git ignore patterns
└── src/                     # Source code directory
    ├── HueHordes/          # Horde spawning mod for Vintage Story
    │   ├── HueHordes/      # Main mod project
    │   ├── HueHordes.Test/ # Comprehensive test suite
    │   ├── CakeBuild/      # Build automation
    │   ├── Main.sln        # Solution file
    │   ├── build.ps1       # Build script
    │   └── README.md       # Mod-specific documentation
    └── [Future Mods]/      # Additional mods will be added here
```

Each mod is:

- 🔧 **Professionally developed** with clean, maintainable code
- 🧪 **Thoroughly tested** for stability and compatibility
- 📚 **Well-documented** with comprehensive guides
- 🎯 **Player-focused** with configurable options
- 🔄 **Actively maintained** with regular updates

---

## 📦 Available Mods

| Mod Name | Status | Documentation | Description |
|----------|--------|---------------|-------------|
| **🧟 HueHordes** | ✅ **Stable** | [README](src/HueHordes/README.md) | Enhanced horde spawning system with intelligent AI, base detection, and health-based door destruction for challenging survival gameplay |

### 🔄 Status Legend

- ✅ **Stable**: Ready for production use
- 🧪 **Beta**: Feature complete, testing phase
- 🚧 **Alpha**: Early development, experimental
- 📝 **Planned**: Concept stage, coming soon

### 🧟 HueHordes Features

**Current Implementation:**

- **🤖 Intelligent AI System** - Clean AI behavior with pathfinding and target detection
- **🏠 Smart Base Detection** - Automatic detection of player structures and bases
- **🎯 Dynamic Targeting** - Advanced targeting system that ignores creative/spectator players
- **🚪 Health-Based Door Destruction** - Doors have 2000HP, max 3 attackers per door
- **⚡ Optimized Performance** - 5-tick update intervals with stuck detection
- **🎮 Command System** - In-game commands for spawning and testing
- **🔧 Configurable Settings** - Debug logging and performance tuning

**Technical Features:**

- **A* Pathfinding** - 3D pathfinding with obstacle avoidance and jump mechanics
- **Line of Sight** - Realistic vision checks for target detection
- **Entity State Management** - Clean state machine for AI behavior
- **Door Health Manager** - Concurrent attacker limits and health tracking
- **Debug Logging** - Comprehensive logging system for troubleshooting

## Development Standards

All mods in this repository follow strict development standards:

### 🏗️ Architecture

- **Modern .NET** - Latest framework versions with nullable reference types
- **Clean Code** - SOLID principles and maintainable design
- **Performance Optimized** - Efficient tick-based updates and memory management
- **Modular Design** - Separated concerns with clear interfaces

### 🧪 Testing

- **Unit Testing** - Comprehensive test coverage for core functionality
- **Integration Testing** - End-to-end testing scenarios
- **Performance Testing** - Load testing and optimization validation
- **Manual Testing** - In-game validation and user experience testing

### 📦 Build & Deployment

- **Automated Builds** - Consistent build process with dotnet CLI
- **Version Management** - Semantic versioning (SemVer)
- **Documentation** - Comprehensive README files and inline documentation
- **Cross-Platform** - Windows, macOS, and Linux compatibility

### 🔧 Code Quality

- **Static Analysis** - Code analysis and best practices
- **Error Handling** - Comprehensive exception handling and logging
- **Security** - Defensive programming practices
- **Performance** - Optimized algorithms and memory usage

## Getting Started

### Prerequisites

- **.NET 8 SDK** or later
- **Vintage Story** - Compatible with version 1.21.1+
- **Visual Studio 2022**, **VS Code**, or **JetBrains Rider** (recommended)

### Quick Start

1. **Clone the repository:**

   ```bash
   git clone https://github.com/HueByte/VintageHue.git
   cd VintageHue
   ```

2. **Navigate to desired mod:**

   ```bash
   cd src/HueHordes  # Example: HueHordes mod
   ```

3. **Build the mod:**

   ```bash
   cd HueHordes
   dotnet build
   ```

4. **Install the mod:**
   - Copy the built DLL from `bin/Debug/Mods/mod/` to your Vintage Story mods folder
   - Or use the provided build scripts for automated installation

### Development Workflow

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Write** tests for new functionality
4. **Implement** the feature
5. **Ensure** all tests pass (`dotnet test`)
6. **Build** the mod (`dotnet build`)
7. **Test** in-game functionality
8. **Commit** changes (`git commit -m 'Add amazing feature'`)
9. **Push** to branch (`git push origin feature/amazing-feature`)
10. **Open** a Pull Request

## 🤝 Contributing

We welcome contributions from the community! Whether you're:

- 🐛 **Reporting bugs**
- 💡 **Suggesting features**
- 🔧 **Submitting code improvements**
- 📖 **Improving documentation**
- 🎨 **Creating assets**

[![Contributor Covenant](https://img.shields.io/badge/Contributor%20Covenant-2.1-4baaaa.svg?style=flat-square)](CODE_OF_CONDUCT.md)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](http://makeapullrequest.com)

### 🎯 Ways to Contribute

- **Code**: Submit pull requests for bug fixes and new features
- **Testing**: Help test mods across different game versions
- **Documentation**: Improve guides, tutorials, and API docs
- **Translation**: Help localize mods for international players
- **Community**: Help other users in discussions and issues

## 💬 Community & Support

### 🆘 Getting Help

- 📖 **Documentation**: Check mod-specific docs linked above
- 🐛 **Bug Reports**: [Open an issue](https://github.com/HueByte/VintageHue/issues/new/choose)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/HueByte/VintageHue/discussions)
- 📧 **Contact**: [Create an issue for direct contact](https://github.com/HueByte/VintageHue/issues)

### 🌐 Links

[![GitHub](https://img.shields.io/badge/GitHub-HueByte-black?style=flat-square&logo=github)](https://github.com/HueByte)
[![Steam Workshop](https://img.shields.io/badge/Steam-Workshop-blue?style=flat-square&logo=steam)](https://steamcommunity.com/id/HueByte/)

### 🏆 Acknowledgments

- **Anego Studios** - For creating the amazing Vintage Story game
- **VS Modding Community** - For resources, tools, and inspiration
- **Contributors** - Everyone who helps improve these mods
- **Players** - For feedback, testing, and support

## Roadmap

### Planned Features

- 🔄 **Enhanced AI Behaviors** - More sophisticated AI patterns
- 🌍 **Multiplayer Optimizations** - Better server performance
- 🎮 **Additional Game Modes** - New spawning patterns and challenges
- 🔧 **Configuration UI** - In-game configuration interface

### Future Mods

We're always working on new mods for Vintage Story. Stay tuned for:

- Quality-of-life improvements
- New content and mechanics
- Performance enhancements
- Multiplayer features

## 📄 License & Legal

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

### 📜 Key Points

- ✅ **Free to use** for personal and commercial projects
- ✅ **Modify and distribute** with attribution
- ✅ **Private use** allowed
- ❌ **No warranty** or liability from authors

### 🎮 Vintage Story Compatibility

These mods are designed for Vintage Story and require the base game. Vintage Story is developed by Anego Studios.

---

<div align="center">

**Made with 💖 by [HueByte](https://github.com/HueByte)**

*Enhancing Vintage Story, one mod at a time* 🎮✨

[![GitHub stars](https://img.shields.io/github/stars/HueByte/VintageHue.svg?style=social&label=Star)](https://github.com/HueByte/VintageHue)
[![GitHub forks](https://img.shields.io/github/forks/HueByte/VintageHue.svg?style=social&label=Fork)](https://github.com/HueByte/VintageHue/fork)
[![GitHub watchers](https://img.shields.io/github/watchers/HueByte/VintageHue.svg?style=social&label=Watch)](https://github.com/HueByte/VintageHue)

[![Back to Top](https://img.shields.io/badge/Back%20to%20Top-↑-blue?style=flat-square)](#-vintagehue---vintage-story-mod-collection)

</div>
