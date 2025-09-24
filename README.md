# HueByte Mods Repository

Welcome to the **HueByte Mods** repository - a collection of high-quality, open-source modifications for various games, developed by HueByte.

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

## Current Mods

### 🧟 HueHordes - Vintage Story Horde Mod

**Location:** `src/HueHordes/`
**Game:** Vintage Story
**Status:** ✅ Active Development

An advanced horde spawning modification for Vintage Story that features:

- **🤖 Modern Async AI System** - Task-based asynchronous programming with .NET 8
- **🏠 Smart Base Detection** - Intelligent detection of player structures and bases
- **🎯 Dynamic Targeting** - Advanced AI targeting system with priority-based selection
- **⚡ High Performance** - Concurrent processing with semaphores and channels
- **🔧 Hot-Reload Configuration** - Real-time configuration updates with FileSystemWatcher
- **🧪 Comprehensive Testing** - 33+ unit and integration tests with 100% pass rate
- **📊 Performance Monitoring** - Built-in metrics and statistics tracking

**Key Features:**

- Spawns hordes outside detected player bases (not inside them!)
- Configurable spawn intervals, entity types, and behavior
- Advanced pathfinding and line-of-sight calculations
- Patrol behavior when targets are lost
- Full async/await implementation for smooth gameplay

**Tech Stack:** C# (.NET 8), xUnit, FluentAssertions, Moq, Cake Build

[📖 View HueHordes Documentation](src/HueHordes/README.md)

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
   git clone https://github.com/HueByte/HueHordes.git
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

## Contributing

We welcome contributions from the community! Please read contributing guidelines before submitting pull requests.

### Types of Contributions

- 🐛 **Bug Reports** - Help us identify and fix issues
- ✨ **Feature Requests** - Suggest new functionality
- 🔧 **Code Contributions** - Submit bug fixes or new features
- 📖 **Documentation** - Improve documentation and examples
- 🧪 **Testing** - Add or improve test coverage

### Code Style

- Follow existing code patterns and conventions
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Write unit tests for new functionality
- Ensure all tests pass before submitting

## Support

### Getting Help

- 📖 **Documentation** - Check the mod-specific README files
- 🐛 **Issues** - [Report bugs or request features](https://github.com/HueByte/HueHordes/issues)
- 💬 **Discussions** - [Community discussions and Q&A](https://github.com/HueByte/HueHordes/discussions)

### Community

- **Discord** - [Join our Discord server](https://discord.gg/your-server) (if applicable)
- **Steam Workshop** - Find published mods on respective game workshops

## Roadmap

### Planned Features

- 🔄 **Auto-Update System** - Automatic mod updates
- 🌍 **Multiplayer Enhancements** - Better multiplayer compatibility
- 📊 **Web Dashboard** - Real-time statistics and configuration
- 🎮 **Additional Games** - Support for more games and platforms

### Future Mods

We're always working on new mods for various games. Stay tuned for:

- Additional Vintage Story mods
- Minecraft modifications
- Other survival and strategy games

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- **Game Developers** - Thank you for creating amazing games and modding APIs
- **Community** - Thanks to all contributors, testers, and users
- **Open Source** - Built with and inspired by the open-source community

---

<div align="center">

**Made with ❤️ by HueByte**

[![GitHub stars](https://img.shields.io/github/stars/HueByte/HueHordes.svg?style=social&label=Star)](https://github.com/HueByte/HueHordes)
[![GitHub forks](https://img.shields.io/github/forks/HueByte/HueHordes.svg?style=social&label=Fork)](https://github.com/HueByte/HueHordes/fork)
[![GitHub watchers](https://img.shields.io/github/watchers/HueByte/HueHordes.svg?style=social&label=Watch)](https://github.com/HueByte/HueHordes)

</div>
