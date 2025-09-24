# HueHordes - Vintage Story Mod

A Vintage Story mod that implements a periodic horde spawning system. The mod spawns waves of hostile entities around players at configurable intervals, adding dynamic challenge to the game.

## Features

### Core Horde System
- **Periodic Horde Events**: Spawns hordes every 3 days by default (configurable)
- **Per-Player Tracking**: Each player has independent horde timers
- **Admin Commands**: Full control over horde timing and testing
- **Configurable Everything**: Spawn counts, timing, radius, entity types, and behavior

### Advanced AI System
- **Player Base Detection**: Automatically detects enclosed areas, walls, and gates
- **Smart Spawning**: Entities spawn outside detected player bases, not inside them
- **Intelligent Targeting**: Dynamic target switching between players, base entrances, and patrol areas
- **Bed-Based Base Centers**: Uses player beds as focal points for base detection and patrol
- **Line-of-Sight Awareness**: Entities adapt when losing visual contact with targets
- **Patrol Behavior**: When no targets available, entities patrol around the player's base
- **Fallback Systems**: Graceful degradation to simple behavior if advanced AI fails

## Prerequisites

- **.NET 8.0 SDK**: Required for building the mod
- **Vintage Story**: Game version 1.21.1 or compatible
- **VINTAGE_STORY Environment Variable**: Must point to your Vintage Story installation directory

### Setting Up Environment Variable

**Windows:**

```cmd
setx VINTAGE_STORY "C:\Path\To\Your\VintageStory\Installation"
```

**Linux/macOS:**

```bash
export VINTAGE_STORY="/path/to/your/vintagestory/installation"
```

## Building the Mod

### Quick Build

Navigate to the mod directory and run the build script:

**Windows:**

```cmd
cd src/HueHordes
./build.ps1
```

**Linux/macOS:**

```bash
cd src/HueHordes
./build.sh
```

### Available Build Tasks

- **Default Build**: `./build.ps1` - Validates JSON, builds, and packages
- **JSON Validation Only**: `./build.ps1 ValidateJson`
- **Build Only**: `./build.ps1 Build`
- **Package Only**: `./build.ps1 Package`
- **Skip JSON Validation**: `./build.ps1 --skipJsonValidation`

### Build Output

Successful builds create:

- Compiled mod: `HueHordes/bin/Release/Mods/mod/`
- Release package: `../Releases/huehordes_1.0.0.zip`

## Installation

1. **Build the mod** (see above) or download a release package
2. **Locate your Vintage Story mods folder**:
   - Windows: `%APPDATA%/VintageStory/Mods/`
   - Linux: `~/.config/VintageStory/Mods/`
   - macOS: `~/Library/Application Support/VintageStory/Mods/`
3. **Install the mod**:
   - Extract `huehordes_1.0.0.zip` to the mods folder, or
   - Copy the entire `huehordes` folder from the build output
4. **Start Vintage Story** and verify the mod loads in the mod list

## Configuration

The mod creates a configuration file at `ModConfig/Horde.server.json` with these settings:

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

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `DaysBetweenHordes` | In-game days between horde events | 3 |
| `Count` | Number of entities per horde | 8 |
| `SpawnRadiusMin` | Minimum spawn distance from player | 12.0 |
| `SpawnRadiusMax` | Maximum spawn distance from player | 24.0 |
| `EntityCodes` | Array of entity types to spawn | `["drifter-normal"]` |
| `NudgeTowardInitialPos` | Enable temporary movement toward player | true |
| `NudgeSeconds` | Duration of nudge behavior | 20.0 |
| `NudgeSpeed` | Movement speed multiplier | 0.05 |

## Admin Commands

Server administrators can use these commands (requires `controlserver` privilege):

### Available Commands

- **`/horde now`** - Trigger immediate horde for the calling player
- **`/horde reset`** - Clear all player horde timers
- **`/horde setdays <number>`** - Set days between hordes (minimum 1)
- **`/horde status`** - Show configuration and all player timer status
- **`/horde spawn <playername>`** - Spawn a horde for a specific player
- **`/horde aiinfo`** - Show AI system information and detected player bases
- **`/horde refreshbase <playername>`** - Force refresh base detection for a player

### Command Examples

```
/horde now                    # Spawn horde for yourself
/horde reset                  # Reset all player timers
/horde setdays 5              # Set hordes to occur every 5 days
/horde status                 # Show detailed status information
/horde spawn PlayerName       # Spawn horde for specific player
/horde aiinfo                 # Show AI system and base detection status
/horde refreshbase PlayerName # Force refresh base detection
```

### Command Features

- **Proper argument parsing**: Uses Vintage Story's command system with type validation
- **Comprehensive help**: Each command has detailed descriptions visible with `/help horde`
- **Error handling**: Clear error messages for invalid arguments or missing players
- **Status reporting**: Detailed information about configuration and player states

### Help System Integration

The mod integrates with Vintage Story's help system. Use these commands to get in-game help:

```
/help horde              # Show all horde commands
/help horde now          # Show help for specific subcommand
/help horde setdays      # Show help for setdays command
```

Each command includes comprehensive descriptions explaining:
- What the command does
- When to use it
- Expected parameters
- Impact on gameplay

## Development

### Project Structure

```
src/HueHordes/
├── HueHordes/              # Main mod project
│   ├── HueHordesModSystem.cs    # Core mod implementation
│   ├── Models/             # Data models
│   │   ├── ServerConfig.cs      # Configuration model
│   │   ├── HordeSaveData.cs     # Save data structure
│   │   └── HordeState.cs        # Per-player state
│   ├── modinfo.json        # Mod metadata
│   └── HueHordes.csproj    # Project file
├── CakeBuild/              # Build system
│   ├── Program.cs          # Build tasks and logic
│   └── CakeBuild.csproj    # Build project
└── Main.sln               # Solution file
```

### VS Code Setup

The project includes VS Code configuration:

- **Build Task**: `Ctrl+Shift+P` → "Tasks: Run Task" → "build"
- **Debug Configuration**: Available for the CakeBuild project
- **Watch Mode**: Available through tasks.json

### Adding New Entity Types

1. Find entity codes in Vintage Story's asset files or other mods
2. Add them to the `EntityCodes` array in the configuration
3. Examples: `"drifter-corrupt"`, `"wolf-male"`, `"locust-basic"`

### Extending the Mod

Key extension points:

- **Custom Entity Behaviors**: Add new `EntityBehavior` classes
- **Spawn Logic**: Modify `SpawnHordeFor()` method
- **Timing Systems**: Extend the tick-based scheduling
- **Command System**: Add new admin commands

## Troubleshooting

### Build Issues

**"VintagestoryAPI.dll not found"**

- Ensure `VINTAGE_STORY` environment variable is set correctly
- Verify VintagestoryAPI.dll exists in the installation directory

**"JSON validation failed"**

- Check all JSON files for syntax errors
- Use `--skipJsonValidation` flag to bypass if needed

### Runtime Issues

**Mod doesn't load**

- Check Vintage Story logs for error messages
- Verify mod is in the correct mods directory
- Ensure game version compatibility (1.21.1)

**Hordes not spawning**

- Check configuration file exists and is valid
- Use `/horde now` to test immediate spawning
- Verify entity codes are valid

**Entities spawn but don't move toward player**

- Check `NudgeTowardInitialPos` is enabled
- Verify `NudgeSeconds` and `NudgeSpeed` values
- Some entity types may not support the nudge behavior

## License

This project is open source. Refer to the license file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly with Vintage Story
5. Submit a pull request

## Support

- Check the [Vintage Story Modding Wiki](https://wiki.vintagestory.at/Modding:Getting_Started) for general modding help
- Review the [API Documentation](https://apidocs.vintagestory.at/) for technical details
- Report issues through the project's issue tracker
