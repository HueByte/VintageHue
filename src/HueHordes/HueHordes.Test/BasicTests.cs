namespace HueHordes.Test;

/// <summary>
/// Basic tests to verify the test framework is working correctly
/// These tests don't depend on Vintage Story API and should always pass
/// </summary>
public class BasicTests
{
    [Fact]
    public void TestFramework_IsWorking()
    {
        // Arrange
        var expected = true;

        // Act
        var actual = true;

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void FluentAssertions_IsWorking()
    {
        // Arrange
        var numbers = new List<int> { 1, 2, 3, 4, 5 };

        // Act & Assert
        numbers.Should().HaveCount(5);
        numbers.Should().Contain(3);
        numbers.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Moq_IsWorking()
    {
        // Arrange
        var mockList = new Mock<IList<string>>();
        mockList.Setup(x => x.Count).Returns(5);
        mockList.Setup(x => x[0]).Returns("first");

        // Act
        var list = mockList.Object;

        // Assert
        list.Count.Should().Be(5);
        list[0].Should().Be("first");
        mockList.Verify(x => x.Count, Times.Once);
    }

    [Fact]
    public async Task AsyncTesting_IsWorking()
    {
        // Arrange
        var delay = 10;

        // Act
        var start = DateTime.UtcNow;
        await Task.Delay(delay);
        var end = DateTime.UtcNow;

        // Assert
        var elapsed = end - start;
        elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromMilliseconds(delay - 5));
        elapsed.Should().BeLessOrEqualTo(TimeSpan.FromMilliseconds(delay + 50));
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(10, 20, 30)]
    [InlineData(-1, 5, 4)]
    public void ParameterizedTesting_IsWorking(int a, int b, int expected)
    {
        // Act
        var result = a + b;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ExceptionTesting_IsWorking()
    {
        // Act & Assert
        Action act = () => throw new InvalidOperationException("Test exception");
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Test exception");
    }

    [Fact]
    public void Collections_Testing_IsWorking()
    {
        // Arrange
        var originalList = new List<string> { "apple", "banana", "cherry" };

        // Act
        var modifiedList = originalList
            .Where(fruit => fruit.Length > 5)
            .Select(fruit => fruit.ToUpper())
            .ToList();

        // Assert
        modifiedList.Should().HaveCount(2);
        modifiedList.Should().Contain("BANANA");
        modifiedList.Should().Contain("CHERRY");
        modifiedList.Should().NotContain("APPLE");
    }
}