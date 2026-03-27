using Microsoft.Extensions.Logging;

namespace SharpFunctional.MsSql;

internal static partial class FunctionalMsSqlDbLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Debug, Message = "Starting EF transaction for result type {ResultType}")]
    internal static partial void StartingEfTransaction(ILogger logger, string resultType);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug, Message = "Committed EF transaction for result type {ResultType}")]
    internal static partial void CommittedEfTransaction(ILogger logger, string resultType);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Rolled back EF transaction due to failed result for type {ResultType}")]
    internal static partial void RolledBackEfTransaction(ILogger logger, string resultType);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "EF transaction failed for result type {ResultType}")]
    internal static partial void EfTransactionFailed(ILogger logger, string resultType, Exception exception);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug, Message = "Starting Dapper transaction for result type {ResultType}")]
    internal static partial void StartingDapperTransaction(ILogger logger, string resultType);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Debug, Message = "Committed Dapper transaction for result type {ResultType}")]
    internal static partial void CommittedDapperTransaction(ILogger logger, string resultType);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Warning, Message = "Rolled back Dapper transaction due to failed result for type {ResultType}")]
    internal static partial void RolledBackDapperTransaction(ILogger logger, string resultType);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Error, Message = "Dapper transaction failed for result type {ResultType}")]
    internal static partial void DapperTransactionFailed(ILogger logger, string resultType, Exception exception);

    [LoggerMessage(EventId = 1020, Level = LogLevel.Debug, Message = "Opened SQL connection after {AttemptCount} attempt(s)")]
    internal static partial void OpenedSqlConnection(ILogger logger, int attemptCount);

    [LoggerMessage(EventId = 1021, Level = LogLevel.Warning, Message = "Transient SQL open failure on attempt {Attempt}. Retrying in {DelayMs} ms")]
    internal static partial void TransientSqlOpenFailure(ILogger logger, int attempt, double delayMs, Exception exception);

    [LoggerMessage(EventId = 1022, Level = LogLevel.Error, Message = "SQL connection open failed after {AttemptCount} attempt(s)")]
    internal static partial void SqlConnectionOpenFailed(ILogger logger, int attemptCount, Exception exception);
}
