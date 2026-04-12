using Microsoft.Extensions.Logging;

namespace SharpFunctional.MsSql.Dapper;

internal static partial class DapperFunctionalDbLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "Executing stored procedure {ProcName} for single value type {ResultType}")]
    internal static partial void ExecutingStoredProcSingle(ILogger logger, string procName, string resultType);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Stored procedure {ProcName} failed for single value type {ResultType}")]
    internal static partial void StoredProcSingleFailed(ILogger logger, string procName, string resultType, Exception exception);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Debug, Message = "Executing stored procedure {ProcName} for sequence type {ResultType}")]
    internal static partial void ExecutingStoredProcSeq(ILogger logger, string procName, string resultType);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Error, Message = "Stored procedure {ProcName} failed for sequence type {ResultType}")]
    internal static partial void StoredProcSeqFailed(ILogger logger, string procName, string resultType, Exception exception);

    [LoggerMessage(EventId = 2020, Level = LogLevel.Debug, Message = "Executing non-query stored procedure {ProcName}")]
    internal static partial void ExecutingStoredProcNonQuery(ILogger logger, string procName);

    [LoggerMessage(EventId = 2021, Level = LogLevel.Error, Message = "Non-query stored procedure {ProcName} failed")]
    internal static partial void StoredProcNonQueryFailed(ILogger logger, string procName, Exception exception);

    [LoggerMessage(EventId = 2030, Level = LogLevel.Debug, Message = "Executing SQL query for single value type {ResultType}")]
    internal static partial void ExecutingQuerySingle(ILogger logger, string resultType);

    [LoggerMessage(EventId = 2031, Level = LogLevel.Error, Message = "SQL query failed for single value type {ResultType}")]
    internal static partial void QuerySingleFailed(ILogger logger, string resultType, Exception exception);

    [LoggerMessage(EventId = 2040, Level = LogLevel.Debug, Message = "Executing SQL query for sequence type {ResultType}")]
    internal static partial void ExecutingQuerySeq(ILogger logger, string resultType);

    [LoggerMessage(EventId = 2041, Level = LogLevel.Error, Message = "SQL query failed for sequence type {ResultType}")]
    internal static partial void QuerySeqFailed(ILogger logger, string resultType, Exception exception);

    [LoggerMessage(EventId = 2050, Level = LogLevel.Debug, Message = "Executing paginated stored procedure {ProcName} page {PageNumber} size {PageSize}")]
    internal static partial void ExecutingStoredProcPaginated(ILogger logger, string procName, int pageNumber, int pageSize);

    [LoggerMessage(EventId = 2051, Level = LogLevel.Error, Message = "Paginated stored procedure {ProcName} failed")]
    internal static partial void StoredProcPaginatedFailed(ILogger logger, string procName, Exception exception);

    [LoggerMessage(EventId = 2060, Level = LogLevel.Warning, Message = "Transient SQL failure on operation {OperationName} attempt {Attempt}. Retrying in {DelayMs} ms")]
    internal static partial void TransientSqlOperationFailure(ILogger logger, string operationName, int attempt, double delayMs, Exception exception);
}
