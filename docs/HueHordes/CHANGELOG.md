# Changelog

All notable changes to HueHordes will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Fixed

### Removed

## [1.0.0] - 2024-09-24

Initial release of HueHordes mod for Vintage Story.

### Added

- **Core horde spawning system** - Periodic wave spawning every 3 days (configurable)
- **Player-specific tracking** - Individual horde timers for each player
- **Configurable spawn parameters** - Adjustable timing, count, radius, and entity types
- **Admin command system** - `/horde now|reset|setdays <n>` for server administration
- **Entity nudge behavior** - Temporary guidance of spawned entities toward player position
- **JSON configuration system** - Server-side configuration via `Horde.server.json`
- **Save data integration** - Persistent storage using Vintage Story's native save system
- **Cross-platform support** - Works on Windows, Linux, and macOS
- **ModSystem architecture** - Follows Vintage Story modding best practices

### Configuration Options

- `DaysBetweenHordes`: Days between horde events (default: 3)
- `Count`: Number of mobs per horde (default: 8)
- `SpawnRadiusMin/Max`: Spawn distance from player (default: 12-24 blocks)
- `EntityCodes`: Array of entity types to spawn (default: ["drifter-normal"])
- `NudgeTowardInitialPos`: Enable temporary movement toward player (default: true)
- `NudgeSeconds`: Duration of nudge behavior (default: 20s)
- `NudgeSpeed`: Movement speed multiplier (default: 0.05)

### Technical Details

- **Framework**: .NET 8.0
- **API**: Vintage Story ModSystem architecture
- **Dependencies**: VintagestoryAPI.dll, Newtonsoft.Json
- **Compatibility**: Vintage Story 1.21.1+

[Unreleased]: https://github.com/HueByte/HueHordes/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/HueByte/HueHordes/releases/tag/v1.0.0
