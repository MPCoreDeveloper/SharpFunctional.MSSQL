namespace SharpFunctional.MsSql.Common;

/// <summary>
/// Configures SQL command timeout and transient retry behavior for Dapper-backed operations.
/// </summary>
/// <param name="commandTimeoutSeconds">Command timeout in seconds.</param>
/// <param name="maxRetryCount">Maximum retry attempts for transient failures.</param>
/// <param name="baseRetryDelay">Base retry delay used for exponential backoff.</param>
/// <param name="maxRetryDelay">Maximum retry delay cap.</param>
public sealed class SqlExecutionOptions(
    int commandTimeoutSeconds = 30,
    int maxRetryCount = 2,
    TimeSpan? baseRetryDelay = null,
    TimeSpan? maxRetryDelay = null)
{
    /// <summary>
    /// Default SQL execution options.
    /// </summary>
    public static SqlExecutionOptions Default { get; } = new();

    /// <summary>
    /// SQL command timeout in seconds.
    /// </summary>
    public int CommandTimeoutSeconds { get; } = commandTimeoutSeconds > 0
        ? commandTimeoutSeconds
        : throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds), "Command timeout must be greater than zero.");

    /// <summary>
    /// Maximum retry attempts for transient failures.
    /// </summary>
    public int MaxRetryCount { get; } = maxRetryCount >= 0
        ? maxRetryCount
        : throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "Max retry count cannot be negative.");

    /// <summary>
    /// Base retry delay used for exponential backoff.
    /// </summary>
    public TimeSpan BaseRetryDelay { get; } = (baseRetryDelay ?? TimeSpan.FromMilliseconds(100)) > TimeSpan.Zero
        ? (baseRetryDelay ?? TimeSpan.FromMilliseconds(100))
        : throw new ArgumentOutOfRangeException(nameof(baseRetryDelay), "Base retry delay must be greater than zero.");

    /// <summary>
    /// Maximum retry delay cap.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; } = (maxRetryDelay ?? TimeSpan.FromSeconds(2)) >= (baseRetryDelay ?? TimeSpan.FromMilliseconds(100))
        ? (maxRetryDelay ?? TimeSpan.FromSeconds(2))
        : throw new ArgumentOutOfRangeException(nameof(maxRetryDelay), "Max retry delay must be greater than or equal to base retry delay.");

    /// <summary>
    /// Calculates exponential backoff delay for a retry attempt.
    /// </summary>
    /// <param name="attempt">Current attempt number (starting at 1).</param>
    /// <returns>The computed retry delay.</returns>
    internal TimeSpan GetRetryDelay(int attempt)
    {
        if (attempt <= 0)
        {
            return BaseRetryDelay;
        }

        var delayMs = BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var cappedMs = Math.Min(delayMs, MaxRetryDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(cappedMs);
    }
}
