using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace SharpFunctional.MsSql.Common;

/// <summary>
/// Represents the operating state of a <see cref="CircuitBreaker"/>.
/// </summary>
public enum CircuitState
{
    /// <summary>Normal operation — all requests are allowed through.</summary>
    Closed,

    /// <summary>Failure threshold exceeded — requests are rejected immediately.</summary>
    Open,

    /// <summary>Recovery probe — a limited number of requests are allowed to test recovery.</summary>
    HalfOpen
}

/// <summary>
/// Configuration for <see cref="CircuitBreaker"/> behavior.
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Number of consecutive failures required to trip the breaker from
    /// <see cref="CircuitState.Closed"/> to <see cref="CircuitState.Open"/>.
    /// Default is 5.
    /// </summary>
    public int FailureThreshold { get; init; } = 5;

    /// <summary>
    /// Duration the breaker stays <see cref="CircuitState.Open"/> before transitioning
    /// to <see cref="CircuitState.HalfOpen"/>. Default is 30 seconds.
    /// </summary>
    public TimeSpan OpenDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of consecutive successes in <see cref="CircuitState.HalfOpen"/> required
    /// to close the breaker. Default is 2.
    /// </summary>
    public int SuccessThresholdInHalfOpen { get; init; } = 2;
}

/// <summary>
/// Implements the circuit breaker pattern for database operations.
/// Thread-safe via the C# 14 <see cref="Lock"/> class.
/// </summary>
/// <example>
/// <code>
/// var breaker = new CircuitBreaker();
/// var result = await breaker.ExecuteAsync(async ct =>
/// {
///     var user = await db.Ef().GetByIdAsync&lt;User, int&gt;(42, ct);
///     return user.IsSome ? FinSucc(user) : FinFail&lt;Option&lt;User&gt;&gt;(Error.New("not found"));
/// }, cancellationToken);
/// </code>
/// </example>
public sealed class CircuitBreaker(CircuitBreakerOptions? options = null)
{
    private readonly CircuitBreakerOptions _options = options ?? new CircuitBreakerOptions();
    private readonly Lock _stateLock = new();

    private CircuitState _state = CircuitState.Closed;
    private DateTime _openedAtUtc = DateTime.MinValue;
    private int _failureCount;
    private int _halfOpenSuccessCount;

    /// <summary>
    /// Gets the current circuit state.
    /// </summary>
    public CircuitState State
    {
        get
        {
            lock (_stateLock)
            {
                return EvaluateState();
            }
        }
    }

    /// <summary>
    /// Gets the number of consecutive failures recorded.
    /// </summary>
    public int FailureCount
    {
        get
        {
            lock (_stateLock)
            {
                return _failureCount;
            }
        }
    }

    /// <summary>
    /// Executes an operation through the circuit breaker.
    /// When the breaker is <see cref="CircuitState.Open"/>, the operation is rejected immediately.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The operation result or a failure when the circuit is open.</returns>
    public async Task<Fin<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Fin<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        lock (_stateLock)
        {
            var currentState = EvaluateState();

            if (currentState == CircuitState.Open)
            {
                return FinFail<T>(Error.New("Circuit breaker is open. Requests are being rejected."));
            }
        }

        var result = await operation(cancellationToken).ConfigureAwait(false);

        lock (_stateLock)
        {
            if (result.IsFail)
            {
                RecordFailure();
            }
            else
            {
                RecordSuccess();
            }
        }

        return result;
    }

    /// <summary>
    /// Manually resets the breaker to <see cref="CircuitState.Closed"/>.
    /// </summary>
    public void Reset()
    {
        lock (_stateLock)
        {
            _state = CircuitState.Closed;
            _failureCount = 0;
            _halfOpenSuccessCount = 0;
        }
    }

    /// <remarks>Must be called inside <c>lock (_stateLock)</c>.</remarks>
    private CircuitState EvaluateState()
    {
        if (_state == CircuitState.Open && DateTime.UtcNow - _openedAtUtc >= _options.OpenDuration)
        {
            _state = CircuitState.HalfOpen;
            _halfOpenSuccessCount = 0;
        }

        return _state;
    }

    /// <remarks>Must be called inside <c>lock (_stateLock)</c>.</remarks>
    private void RecordFailure()
    {
        _failureCount++;
        _halfOpenSuccessCount = 0;

        if (_state == CircuitState.HalfOpen)
        {
            _state = CircuitState.Open;
            _openedAtUtc = DateTime.UtcNow;
        }
        else if (_state == CircuitState.Closed && _failureCount >= _options.FailureThreshold)
        {
            _state = CircuitState.Open;
            _openedAtUtc = DateTime.UtcNow;
        }
    }

    /// <remarks>Must be called inside <c>lock (_stateLock)</c>.</remarks>
    private void RecordSuccess()
    {
        if (_state == CircuitState.HalfOpen)
        {
            _halfOpenSuccessCount++;

            if (_halfOpenSuccessCount >= _options.SuccessThresholdInHalfOpen)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
                _halfOpenSuccessCount = 0;
            }
        }
        else
        {
            _failureCount = 0;
        }
    }
}
