using System;
using System.Threading;
using System.Threading.Tasks;
using HueHordes.Test.TestHelpers;

namespace HueHordes.Test.AI;

/// <summary>
/// Simple AI system tests that work without complex API dependencies
/// These tests verify basic functionality and patterns used in the async AI system
/// </summary>
public class SimpleAITests
{
    [Fact]
    public void ServerConfig_CanBeCreatedWithValidDefaults()
    {
        // Act
        var config = VintageStoryTestHelper.CreateTestConfig();

        // Assert
        config.Should().NotBeNull();
        config.DaysBetweenHordes.Should().BeGreaterThan(0);
        config.Count.Should().BeGreaterThan(0);
        config.EntityCodes.Should().NotBeEmpty();
        config.SpawnRadiusMin.Should().BeGreaterThan(0);
        config.SpawnRadiusMax.Should().BeGreaterThan(config.SpawnRadiusMin);
        config.NudgeSeconds.Should().BeGreaterThan(0);
        config.NudgeSpeed.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ServerConfig_PropertiesCanBeModified()
    {
        // Arrange
        var config = VintageStoryTestHelper.CreateTestConfig();

        // Act
        config.DaysBetweenHordes = 14;
        config.Count = 20;
        config.EntityCodes = new[] { "game:wolf", "game:bear" };

        // Assert
        config.DaysBetweenHordes.Should().Be(14);
        config.Count.Should().Be(20);
        config.EntityCodes.Should().Equal(new[] { "game:wolf", "game:bear" });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    public void ServerConfig_DaysBetweenHordes_AcceptsValidValues(int days)
    {
        // Arrange
        var config = VintageStoryTestHelper.CreateTestConfig();

        // Act
        config.DaysBetweenHordes = days;

        // Assert
        config.DaysBetweenHordes.Should().Be(days);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    public void ServerConfig_Count_AcceptsValidValues(int count)
    {
        // Arrange
        var config = VintageStoryTestHelper.CreateTestConfig();

        // Act
        config.Count = count;

        // Assert
        config.Count.Should().Be(count);
    }

    [Fact]
    public void ServerConfig_JsonSerialization_WorksCorrectly()
    {
        // Arrange
        var originalConfig = VintageStoryTestHelper.CreateTestConfig();
        originalConfig.DaysBetweenHordes = 12;
        originalConfig.Count = 8;
        originalConfig.EntityCodes = new[] { "test:entity1", "test:entity2" };

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(originalConfig);
        var deserializedConfig = System.Text.Json.JsonSerializer.Deserialize<ServerConfig>(json);

        // Assert
        deserializedConfig.Should().NotBeNull();
        deserializedConfig!.DaysBetweenHordes.Should().Be(originalConfig.DaysBetweenHordes);
        deserializedConfig.Count.Should().Be(originalConfig.Count);
        deserializedConfig.EntityCodes.Should().Equal(originalConfig.EntityCodes);
        deserializedConfig.SpawnRadiusMin.Should().Be(originalConfig.SpawnRadiusMin);
        deserializedConfig.SpawnRadiusMax.Should().Be(originalConfig.SpawnRadiusMax);
    }

    [Fact]
    public async Task AsyncOperations_CancellationTokens_WorkCorrectly()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var startTime = DateTime.UtcNow;

        // Act
        var task = SimulateAsyncOperation(cts.Token);
        await Task.Delay(50); // Let it start
        cts.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        var elapsed = DateTime.UtcNow - startTime;
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "Cancellation should be quick");
    }

    [Fact]
    public async Task AsyncOperations_ConcurrentExecution_WorksSafely()
    {
        // Arrange
        var tasks = new List<Task<int>>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(SimulateAsyncWorkload(index));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().BeInAscendingOrder();
        tasks.All(t => t.IsCompletedSuccessfully).Should().BeTrue();
    }

    [Fact]
    public async Task AsyncOperations_WithTimeout_CompletesWithinExpectedTime()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        const int timeoutMs = 200;

        // Act
        var result = await SimulateAsyncWorkloadWithTimeout(timeoutMs);

        // Assert
        var elapsed = DateTime.UtcNow - startTime;
        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(timeoutMs + 100));
        result.Should().Be(42); // Expected result
    }

    [Fact]
    public void TestHelper_CreatesValidPositions()
    {
        // Act
        var (x, y, z) = VintageStoryTestHelper.CreateTestPosition(150, 80, 250);

        // Assert
        x.Should().Be(150);
        y.Should().Be(80);
        z.Should().Be(250);
    }

    [Fact]
    public void TestHelper_APIAvailability_ReportsCorrectly()
    {
        // Act
        var isAvailable = VintageStoryTestHelper.IsVintageStoryAPIAvailable();

        // Assert
        // This will be false unless VINTAGE_STORY environment variable is set
#if VINTAGE_STORY_AVAILABLE
        isAvailable.Should().BeTrue();
#else
        isAvailable.Should().BeFalse();
#endif
    }

    [Fact]
    public void ThreadSafety_ConcurrentAccess_WorksCorrectly()
    {
        // Arrange
        var sharedConfig = VintageStoryTestHelper.CreateTestConfig();
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        // Act - Concurrent read operations
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    // Simulate concurrent reads
                    var days = sharedConfig.DaysBetweenHordes;
                    var count = sharedConfig.Count;
                    var codes = sharedConfig.EntityCodes.Length;

                    // All should be valid values
                    days.Should().BeGreaterThan(0);
                    count.Should().BeGreaterThan(0);
                    codes.Should().BeGreaterThan(0);
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        exceptions.Should().BeEmpty("No exceptions should occur during concurrent reads");
        tasks.All(t => t.IsCompletedSuccessfully).Should().BeTrue();
    }

    /// <summary>
    /// Simulates an async operation that can be cancelled
    /// </summary>
    private async Task<int> SimulateAsyncOperation(CancellationToken cancellationToken)
    {
        for (int i = 0; i < 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(10, cancellationToken);
        }
        return 42;
    }

    /// <summary>
    /// Simulates an async workload with a predictable result
    /// </summary>
    private async Task<int> SimulateAsyncWorkload(int input)
    {
        await Task.Delay(10 + input * 5); // Variable delay
        return input; // Return input to verify ordering
    }

    /// <summary>
    /// Simulates an async operation with timeout
    /// </summary>
    private async Task<int> SimulateAsyncWorkloadWithTimeout(int timeoutMs)
    {
        await Task.Delay(timeoutMs / 2); // Half the timeout
        return 42;
    }
}