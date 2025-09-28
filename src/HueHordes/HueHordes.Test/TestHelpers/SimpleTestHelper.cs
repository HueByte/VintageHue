using System;
using HueHordes.Models;

namespace HueHordes.Test.TestHelpers;

/// <summary>
/// Simple test helper that creates test objects without complex API dependencies
/// </summary>
public static class VintageStoryTestHelper
{
    /// <summary>
    /// Creates test configuration with realistic values
    /// </summary>
    public static ServerConfig CreateTestConfig()
    {
        return new ServerConfig
        {
            DaysBetweenHordes = 7,
            Count = 5,
            SpawnRadiusMin = 15f,
            SpawnRadiusMax = 30f,
            EntityCodes = new[] { "game:drifter-normal", "game:drifter-deep" },
            NudgeTowardInitialPos = true,
            NudgeSeconds = 30f,
            NudgeSpeed = 0.1f
        };
    }

    /// <summary>
    /// Determines if Vintage Story API is available for testing
    /// </summary>
    public static bool IsVintageStoryAPIAvailable()
    {
#if VINTAGE_STORY_AVAILABLE
        return true;
#else
        return false;
#endif
    }

    /// <summary>
    /// Creates a simple test position vector
    /// </summary>
    public static (double x, double y, double z) CreateTestPosition(double x = 100, double y = 64, double z = 100)
    {
        return (x, y, z);
    }

    /// <summary>
    /// Creates a test configuration with specific values for validation testing
    /// </summary>
    public static ServerConfig CreateInvalidTestConfig()
    {
        return new ServerConfig
        {
            DaysBetweenHordes = -1, // Invalid
            Count = 0, // Invalid
            SpawnRadiusMin = -5f, // Invalid
            SpawnRadiusMax = 1f, // Invalid (less than corrected min)
            EntityCodes = new string[0], // Invalid (empty)
            NudgeSeconds = -10f, // Invalid
            NudgeSpeed = -0.5f // Invalid
        };
    }
}