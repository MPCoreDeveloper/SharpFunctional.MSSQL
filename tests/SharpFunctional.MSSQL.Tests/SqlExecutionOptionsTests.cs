using SharpFunctional.MsSql.Common;
using Xunit;

namespace SharpFunctional.MsSql.Tests;

public class SqlExecutionOptionsTests
{
    [Fact]
    public void Constructor_WithDefaults_ShouldSetExpectedValues()
    {
        // Arrange
        var options = new SqlExecutionOptions();

        // Assert
        Assert.Equal(30, options.CommandTimeoutSeconds);
        Assert.Equal(2, options.MaxRetryCount);
        Assert.Equal(TimeSpan.FromMilliseconds(100), options.BaseRetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(2), options.MaxRetryDelay);
    }

    [Fact]
    public void Constructor_WithInvalidCommandTimeout_ShouldThrow()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqlExecutionOptions(commandTimeoutSeconds: 0));
    }

    [Fact]
    public void Constructor_WithNegativeRetryCount_ShouldThrow()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SqlExecutionOptions(maxRetryCount: -1));
    }

    [Fact]
    public void Constructor_WithMaxRetryDelaySmallerThanBase_ShouldThrow()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SqlExecutionOptions(
                baseRetryDelay: TimeSpan.FromMilliseconds(500),
                maxRetryDelay: TimeSpan.FromMilliseconds(100)));
    }
}
