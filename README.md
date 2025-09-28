# 🎮 VintageHue - Vintage Story Mod Collection

[![License](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)
[![GitHub Release](https://img.shields.io/github/v/release/HueByte/VintageHue?style=flat-square)](https://github.com/HueByte/VintageHue/releases)
[![GitHub Stars](https://img.shields.io/github/stars/HueByte/VintageHue?style=flat-square)](https://github.com/HueByte/VintageHue/stargazers)
[![Issues](https://img.shields.io/github/issues/HueByte/VintageHue?style=flat-square)](https://github.com/HueByte/VintageHue/issues)
[![Mod Tests](https://img.shields.io/github/actions/workflow/status/HueByte/VintageHue/mod-tests.yml?branch=master&style=flat-square&label=Mod%20Tests)](https://github.com/HueByte/VintageHue/actions/workflows/mod-tests.yml)

> A curated collection of high-quality mods for Vintage Story, crafted with passion by **HueByte** 🦄

Welcome to **VintageHue** - my personal collection of Vintage Story modifications! This repository houses multiple mods that enhance the Vintage Story experience through innovative gameplay mechanics, quality-of-life improvements, and exciting new content.

## Repository Structure

This repository follows a modular structure where each mod is contained in its own dedicated folder within the `src` directory:

```
HueHordes/
├── README.md                 # This file - repository overview
├── LICENSE                   # Repository license
├── .gitignore               # Git ignore patterns
├── .vscode/                 # VS Code workspace configuration
├── .claude/                 # Claude Code configuration
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

| Mod Name | Build Status | Documentation | Description |
|----------|-------------|---------------|-------------|
| **🧟 HueHordes** | [![Build Status](https://img.shields.io/github/actions/workflow/status/HueByte/VintageHue/mod-tests.yml?branch=master&style=flat-square&label=Build)](https://github.com/HueByte/VintageHue/actions/workflows/mod-tests.yml) | [![Documentation](https://img.shields.io/badge/Docs-GitHub%20Pages-brightgreen?style=flat-square)](https://huebyte.github.io/VintageHue/HueHordes/) | Advanced horde spawning system with intelligent AI that detects player bases and spawns dynamic waves of enemies for challenging survival gameplay |

### 🔄 Status Legend

- ✅ **Stable**: Ready for production use
- 🧪 **Beta**: Feature complete, testing phase
- 🚧 **Alpha**: Early development, experimental
- 📝 **Planned**: Concept stage, coming soon

### 🧟 HueHordes Features

- **🤖 Modern Async AI System** - Task-based asynchronous programming with .NET 8
- **🏠 Smart Base Detection** - Intelligent detection of player structures and bases
- **🎯 Dynamic Targeting** - Advanced AI targeting system with priority-based selection
- **⚡ High Performance** - Concurrent processing with semaphores and channels
- **🔧 Hot-Reload Configuration** - Real-time configuration updates with FileSystemWatcher
- **🧪 Comprehensive Testing** - 33+ unit and integration tests with 100% pass rate
- **📊 Performance Monitoring** - Built-in metrics and statistics tracking

## Development Standards

All mods in this repository follow strict development standards:

### 🏗️ Architecture

- **Modern .NET** - Latest framework versions with nullable reference types
- **Async/Await** - Task-based asynchronous programming (TAP)
- **SOLID Principles** - Clean, maintainable, and extensible code
- **Dependency Injection** - Proper separation of concerns

### 🧪 Testing

- **Comprehensive Test Suites** - Unit, integration, and performance tests
- **High Code Coverage** - Aim for >90% test coverage
- **Continuous Integration** - Automated testing on all commits
- **Test-Driven Development** - Tests written alongside or before implementation

### 📦 Build & Deployment

- **Automated Builds** - Cake Build system with PowerShell scripts
- **Version Management** - Semantic versioning (SemVer)
- **Documentation** - Comprehensive README files and code documentation
- **Cross-Platform** - Windows, macOS, and Linux compatibility where applicable

### 🔧 Code Quality

- **Static Analysis** - Code analysis and linting
- **Performance Monitoring** - Built-in metrics and profiling
- **Error Handling** - Comprehensive exception handling and logging
- **Security** - Defensive programming practices

## Getting Started

### Prerequisites

- **.NET 8 SDK** or later
- **Game-specific requirements** (see individual mod documentation)
- **Visual Studio 2022**, **VS Code**, or **JetBrains Rider** (recommended)

### Quick Start

1. **Clone the repository:**

   ```bash
   git clone https://github.com/HueByte/VintageHue.git
   cd HueHordes
   ```

2. **Navigate to desired mod:**

   ```bash
   cd src/HueHordes  # Example: HueHordes mod
   ```

3. **Build the mod:**

   ```bash
   ./build.ps1       # Windows
   ./build.sh        # Linux/macOS
   ```

4. **Run tests:**

   ```bash
   dotnet test
   ```

### Development Workflow

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Write** tests for new functionality
4. **Implement** the feature
5. **Ensure** all tests pass (`dotnet test`)
6. **Build** the mod (`./build.ps1`)
7. **Commit** changes (`git commit -m 'Add amazing feature'`)
8. **Push** to branch (`git push origin feature/amazing-feature`)
9. **Open** a Pull Request

## 🤝 Contributing

We welcome contributions from the community! Whether you're:

- 🐛 **Reporting bugs**
- 💡 **Suggesting features**
- 🔧 **Submitting code improvements**
- 📖 **Improving documentation**
- 🎨 **Creating assets**

[![Contributor Covenant](https://img.shields.io/badge/Contributor%20Covenant-2.1-4baaaa.svg?style=flat-square)](CODE_OF_CONDUCT.md)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](http://makeapullrequest.com)

### 📝 Contribution Guidelines

Please read our [Contributing Guide](CONTRIBUTING.md) for detailed information on:

- Development setup and workflow
- Code style and standards
- Testing requirements
- Pull request process
- Community guidelines

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
<!-- [![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-ff5e5b?style=flat-square&logo=ko-fi)](https://ko-fi.com/huebyte) -->

### 🏆 Acknowledgments

- **Anego Studios** - For creating the amazing Vintage Story game
- **VS Modding Community** - For resources, tools, and inspiration
- **Contributors** - Everyone who helps improve these mods
- **Players** - For feedback, testing, and support

## Roadmap

### Planned Features

- 🔄 **Auto-Update System** - Automatic mod updates
- 🌍 **Multiplayer Enhancements** - Better multiplayer compatibility

### Future Mods

We're always working on new mods for various games. Stay tuned for:

- Additional Vintage Story mods
- More AI and gameplay enhancements
- Quality-of-life improvements
- New content and mechanics

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
