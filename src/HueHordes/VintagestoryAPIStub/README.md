# VintagestoryAPI Stub

This is a stub implementation of the VintagestoryAPI designed specifically for CI/CD builds and compilation testing.

## ⚠️ Important Notice

**This stub is NOT intended for production use.** It only provides the minimal API surface required for compilation and basic testing. It does not implement actual game functionality.

## Purpose

The VintagestoryAPI.dll cannot be redistributed due to licensing restrictions, but is required for building Vintage Story mods. This stub assembly provides:

- ✅ **Compilation Support**: Allows mods to compile in CI/CD environments
- ✅ **Type Safety**: Maintains correct method signatures and interfaces
- ✅ **Basic Testing**: Enables unit tests that don't require actual game logic
- ❌ **No Game Logic**: Does not implement actual Vintage Story functionality

## Usage

This stub is automatically built and used by the CI/CD pipeline when the real VintagestoryAPI.dll is not available.

### For Local Development

Use the real VintagestoryAPI.dll from your Vintage Story installation:

```bash
export VINTAGE_STORY="/path/to/vintagestory"  # Points to real installation
./build.sh  # Uses real API
```

### For CI/CD

The CI system automatically builds and uses this stub:

```bash
# CI automatically runs this when real API is not available
dotnet build VintagestoryAPIStub/VintagestoryAPIStub.csproj -o lib/
dotnet build  # Now uses the stub from lib/
```

## Included API Coverage

The stub includes minimal implementations for:

- `Vintagestory.API.Common.*` - Core mod system and entities
- `Vintagestory.API.Server.*` - Server-side APIs and player management
- `Vintagestory.API.MathTools.*` - Vector math and positioning
- `Vintagestory.API.Datastructures.*` - Data storage and serialization
- `Vintagestory.API.Config.*` - Configuration and constants
- `Vintagestory.API.Util.*` - Utility functions

## Limitations

- **No Game Logic**: Methods return default values or do nothing
- **No Persistence**: Data is not actually saved or loaded
- **No Networking**: Network calls are ignored
- **No Rendering**: Graphics operations are not implemented
- **No Physics**: Physics and collision detection are minimal
- **No Events**: Event system provides stub implementations only

## Contributing

If you encounter compilation errors due to missing API members, please add the minimal stub implementation to maintain build compatibility.
