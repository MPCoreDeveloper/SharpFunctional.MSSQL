using SharpFunctional.MsSql.Common;
using SharpFunctional.MsSql.Functional;
using Xunit;
using static SharpFunctional.MsSql.Functional.Prelude;

namespace SharpFunctional.MsSql.Tests;

public class CircuitBreakerTests
{
    private static readonly Func<CancellationToken, Task<Fin<int>>> SuccessOp =
        _ => Task.FromResult<Fin<int>>(42);

    private static readonly Func<CancellationToken, Task<Fin<int>>> FailOp =
        _ => Task.FromResult(FinFail<int>(Error.New("boom")));

    // --- Initial state ---

    [Fact]
    public void NewCircuitBreaker_ShouldBeClosedWithZeroFailures()
    {
        // Act
        var breaker = new CircuitBreaker();

        // Assert
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(0, breaker.FailureCount);
    }

    // --- Closed state ---

    [Fact]
    public async Task ExecuteAsync_WhenClosed_ShouldAllowOperations()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var breaker = new CircuitBreaker();

        // Act
        var result = await breaker.ExecuteAsync(SuccessOp, ct);

        // Assert
        Assert.True(result.IsSucc);
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClosedWithFailureBelowThreshold_ShouldStayClosed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var breaker = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 3 });

        // Act
        await breaker.ExecuteAsync(FailOp, ct);
        await breaker.ExecuteAsync(FailOp, ct);

        // Assert
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(2, breaker.FailureCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessResetsFailureCount_ShouldNotTrip()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var breaker = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 3 });

        // Act — 2 failures, then 1 success, then 2 more failures
        await breaker.ExecuteAsync(FailOp, ct);
        await breaker.ExecuteAsync(FailOp, ct);
        await breaker.ExecuteAsync(SuccessOp, ct);
        await breaker.ExecuteAsync(FailOp, ct);
        await breaker.ExecuteAsync(FailOp, ct);

        // Assert — still closed because success reset the counter
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(2, breaker.FailureCount);
    }

    // --- Transition to Open ---

    [Fact]
    public async Task ExecuteAsync_WhenFailuresReachThreshold_ShouldTripToOpen()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var breaker = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 3 });

        // Act
        await breaker.ExecuteAsync(FailOp, ct);
        await breaker.ExecuteAsync(FailOp, ct);
        await breaker.ExecuteAsync(FailOp, ct);

        // Assert
        Assert.Equal(CircuitState.Open, breaker.State);
    }

    // --- Open state ---

    [Fact]
    public async Task ExecuteAsync_WhenOpen_ShouldRejectImmediately()
    {
        // Arrange
        var breaker = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromMinutes(5)
        });
        var ct = TestContext.Current.CancellationToken;
        await breaker.ExecuteAsync(FailOp, ct);

        // Act
        var result = await breaker.ExecuteAsync(SuccessOp, ct);

        // Assert
        Assert.True(result.IsFail);
        Assert.Equal(CircuitState.Open, breaker.State);
    }

    // --- Transition to HalfOpen ---

    [Fact]
    public async Task State_AfterOpenDurationExpires_ShouldTransitionToHalfOpen()
    {
        // Arrange
        var breaker = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromMilliseconds(50)
        });
        var ct = TestContext.Current.CancellationToken;
        await breaker.ExecuteAsync(FailOp, ct);
        Assert.Equal(CircuitState.Open, breaker.State);

        // Act — wait for duration to expire
        await Task.Delay(100, ct);

        // Assert
        Assert.Equal(CircuitState.HalfOpen, breaker.State);
    }

    // --- HalfOpen state ---

    [Fact]
    public async Task ExecuteAsync_WhenHalfOpenAndSucceeds_ShouldCloseAfterThreshold()
    {
        // Arrange
        var breaker = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromMilliseconds(50),
            SuccessThresholdInHalfOpen = 2
        });
        var ct = TestContext.Current.CancellationToken;
        await breaker.ExecuteAsync(FailOp, ct);
        await Task.Delay(100, ct);
        Assert.Equal(CircuitState.HalfOpen, breaker.State);

        // Act — two successes in half-open
        await breaker.ExecuteAsync(SuccessOp, ct);
        await breaker.ExecuteAsync(SuccessOp, ct);

        // Assert
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(0, breaker.FailureCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHalfOpenAndFails_ShouldReopenImmediately()
    {
        // Arrange
        var breaker = new CircuitBreaker(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            OpenDuration = TimeSpan.FromMilliseconds(50)
        });
        var ct = TestContext.Current.CancellationToken;
        await breaker.ExecuteAsync(FailOp, ct);
        await Task.Delay(100, ct);
        Assert.Equal(CircuitState.HalfOpen, breaker.State);

        // Act — fail in half-open
        await breaker.ExecuteAsync(FailOp, ct);

        // Assert
        Assert.Equal(CircuitState.Open, breaker.State);
    }

    // --- Reset ---

    [Fact]
    public async Task Reset_WhenOpen_ShouldReturnToClosed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var breaker = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 1 });
        await breaker.ExecuteAsync(FailOp, ct);
        Assert.Equal(CircuitState.Open, breaker.State);

        // Act
        breaker.Reset();

        // Assert
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(0, breaker.FailureCount);
    }

    // --- Null guard ---

    [Fact]
    public async Task ExecuteAsync_WithNullOperation_ShouldThrowArgumentNullException()
    {
        // Arrange
        var breaker = new CircuitBreaker();

        // Act & Assert
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<ArgumentNullException>(() => breaker.ExecuteAsync<int>(null!, ct));
    }

    // --- Custom options ---

    [Fact]
    public void CircuitBreakerOptions_DefaultValues_ShouldBeReasonable()
    {
        // Act
        var options = new CircuitBreakerOptions();

        // Assert
        Assert.Equal(5, options.FailureThreshold);
        Assert.Equal(TimeSpan.FromSeconds(30), options.OpenDuration);
        Assert.Equal(2, options.SuccessThresholdInHalfOpen);
    }

    [Fact]
    public void GetSnapshot_NewBreaker_ShouldExposeCurrentMetrics()
    {
        // Arrange
        var breaker = new CircuitBreaker();

        // Act
        var snapshot = breaker.GetSnapshot();

        // Assert
        Assert.Equal(CircuitState.Closed, snapshot.State);
        Assert.Equal(0, snapshot.FailureCount);
        Assert.Equal(0, snapshot.HalfOpenSuccessCount);
        Assert.True(snapshot.TimeInState >= TimeSpan.Zero);
    }

    [Fact]
    public async Task GetSnapshot_AfterFailure_ShouldReflectFailureCount()
    {
        // Arrange
        var breaker = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 3 });
        var ct = TestContext.Current.CancellationToken;

        // Act
        await breaker.ExecuteAsync(FailOp, ct);
        var snapshot = breaker.GetSnapshot();

        // Assert
        Assert.Equal(CircuitState.Closed, snapshot.State);
        Assert.Equal(1, snapshot.FailureCount);
    }

    [Fact]
    public async Task GetSnapshot_WhenOpen_ShouldExposeOpenedTimestamp()
    {
        // Arrange
        var breaker = new CircuitBreaker(new CircuitBreakerOptions { FailureThreshold = 1 });
        var ct = TestContext.Current.CancellationToken;

        // Act
        await breaker.ExecuteAsync(FailOp, ct);
        var snapshot = breaker.GetSnapshot();

        // Assert
        Assert.Equal(CircuitState.Open, snapshot.State);
        Assert.NotEqual(DateTime.MinValue, snapshot.OpenedAtUtc);
        Assert.True(snapshot.TimeInState >= TimeSpan.Zero);
    }
}
