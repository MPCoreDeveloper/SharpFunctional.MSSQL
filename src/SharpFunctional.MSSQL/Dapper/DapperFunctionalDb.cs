using System.Data;
using System.Diagnostics;
using global::Dapper;
using Microsoft.Extensions.Logging;
using SharpFunctional.MsSql.Common;
using SharpFunctional.MsSql.Functional;
using static SharpFunctional.MsSql.Functional.Prelude;

namespace SharpFunctional.MsSql.Dapper;

/// <summary>
/// Provides functional Dapper access for SQL and stored procedures.
/// </summary>
public sealed class DapperFunctionalDb(
    IDbConnection? connection,
    FunctionalMsSqlDb owner,
    SqlExecutionOptions? executionOptions = null,
    ILogger? logger = null)
{
    private IDbConnection? Connection => connection;
    private FunctionalMsSqlDb Owner => owner;
    private SqlExecutionOptions Options => executionOptions ?? SqlExecutionOptions.Default;
    private ILogger? Logger => logger;

    /// <summary>
    /// Executes a stored procedure and returns a single optional value.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="procName">The name of the stored procedure to execute.</param>
    /// <param name="param">Parameters to pass to the stored procedure.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    public async Task<Option<T>> ExecuteStoredProcSingleAsync<T>(
        string procName,
        object param,
        CancellationToken cancellationToken = default)
    {
        if (Connection is null || string.IsNullOrWhiteSpace(procName))
        {
            return Option<T>.None;
        }

        using var activity = StartDapperActivity("dapper.storedproc.single", procName);

        try
        {
            Logger?.LogDebug("Executing stored procedure {ProcName} for single value type {ResultType}", procName, typeof(T).Name);
            var result = await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(Connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            procName,
                            param,
                            transaction: Owner.AmbientTransaction,
                            commandTimeout: Options.CommandTimeoutSeconds,
                            commandType: CommandType.StoredProcedure,
                            cancellationToken: ct);

                        return await Connection.QueryFirstOrDefaultAsync<T>(command).ConfigureAwait(false);
                    },
                    cancellationToken,
                    procName)
                .ConfigureAwait(false);

            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return Optional(result);
        }
        catch (Exception exception)
        {
            Logger?.LogError(exception, "Stored procedure {ProcName} failed for single value type {ResultType}", procName, typeof(T).Name);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return Option<T>.None;
        }
    }

    /// <summary>
    /// Executes a stored procedure and returns all rows as a materialized sequence.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="procName">The name of the stored procedure to execute.</param>
    /// <param name="param">Parameters to pass to the stored procedure.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    public async Task<Seq<T>> ExecuteStoredProcAsync<T>(
        string procName,
        object param,
        CancellationToken cancellationToken = default)
    {
        if (Connection is null || string.IsNullOrWhiteSpace(procName))
        {
            return Seq<T>();
        }

        using var activity = StartDapperActivity("dapper.storedproc.seq", procName);

        try
        {
            Logger?.LogDebug("Executing stored procedure {ProcName} for sequence type {ResultType}", procName, typeof(T).Name);
            var result = await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(Connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            procName,
                            param,
                            transaction: Owner.AmbientTransaction,
                            commandTimeout: Options.CommandTimeoutSeconds,
                            commandType: CommandType.StoredProcedure,
                            cancellationToken: ct);

                        return await Connection.QueryAsync<T>(command).ConfigureAwait(false);
                    },
                    cancellationToken,
                    procName)
                .ConfigureAwait(false);

            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return toSeq(result.ToList());
        }
        catch (Exception exception)
        {
            Logger?.LogError(exception, "Stored procedure {ProcName} failed for sequence type {ResultType}", procName, typeof(T).Name);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return Seq<T>();
        }
    }

    /// <summary>
    /// Executes a non-query stored procedure.
    /// </summary>
    /// <param name="procName">The name of the stored procedure to execute.</param>
    /// <param name="param">Parameters to pass to the stored procedure.</param>
    /// <param name="cancellationToken">Token used to cancel the command.</param>
    public async Task<Fin<Unit>> ExecuteStoredProcNonQueryAsync(
        string procName,
        object param,
        CancellationToken cancellationToken = default)
    {
        if (Connection is null)
        {
            return FinFail<Unit>(Error.New("Dapper backend is not configured."));
        }

        if (string.IsNullOrWhiteSpace(procName))
        {
            return FinFail<Unit>(Error.New("Stored procedure name cannot be empty."));
        }

        using var activity = StartDapperActivity("dapper.storedproc.nonquery", procName);

        try
        {
            Logger?.LogDebug("Executing non-query stored procedure {ProcName}", procName);
            await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(Connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            procName,
                            param,
                            transaction: Owner.AmbientTransaction,
                            commandTimeout: Options.CommandTimeoutSeconds,
                            commandType: CommandType.StoredProcedure,
                            cancellationToken: ct);

                        _ = await Connection.ExecuteAsync(command).ConfigureAwait(false);
                        return unit;
                    },
                    cancellationToken,
                    procName)
                .ConfigureAwait(false);

            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return unit;
        }
        catch (Exception exception)
        {
            Logger?.LogError(exception, "Non-query stored procedure {ProcName} failed", procName);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return FinFail<Unit>(Error.New(exception));
        }
    }

    /// <summary>
    /// Executes a SQL query and returns all rows as a materialized sequence.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="param">Parameters to pass to the query.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    public async Task<Seq<T>> QueryAsync<T>(
        string sql,
        object param,
        CancellationToken cancellationToken = default)
    {
        if (Connection is null || string.IsNullOrWhiteSpace(sql))
        {
            return Seq<T>();
        }

        using var activity = StartDapperActivity("dapper.query.seq", sql);

        try
        {
            Logger?.LogDebug("Executing SQL query for sequence type {ResultType}", typeof(T).Name);
            var result = await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(Connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            sql,
                            param,
                            transaction: Owner.AmbientTransaction,
                            commandTimeout: Options.CommandTimeoutSeconds,
                            cancellationToken: ct);

                        return await Connection.QueryAsync<T>(command).ConfigureAwait(false);
                    },
                    cancellationToken,
                    sql)
                .ConfigureAwait(false);

            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return toSeq(result.ToList());
        }
        catch (Exception exception)
        {
            Logger?.LogError(exception, "SQL query failed for sequence type {ResultType}", typeof(T).Name);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return Seq<T>();
        }
    }

    /// <summary>
    /// Executes a SQL query and returns a single optional value.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="param">Parameters to pass to the query.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    public async Task<Option<T>> QuerySingleAsync<T>(
        string sql,
        object param,
        CancellationToken cancellationToken = default)
    {
        if (Connection is null || string.IsNullOrWhiteSpace(sql))
        {
            return Option<T>.None;
        }

        using var activity = StartDapperActivity("dapper.query.single", sql);

        try
        {
            Logger?.LogDebug("Executing SQL query for single value type {ResultType}", typeof(T).Name);
            var result = await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(Connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            sql,
                            param,
                            transaction: Owner.AmbientTransaction,
                            commandTimeout: Options.CommandTimeoutSeconds,
                            cancellationToken: ct);

                        return await Connection.QueryFirstOrDefaultAsync<T>(command).ConfigureAwait(false);
                    },
                    cancellationToken,
                    sql)
                .ConfigureAwait(false);

            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return Optional(result);
        }
        catch (Exception exception)
        {
            Logger?.LogError(exception, "SQL query failed for single value type {ResultType}", typeof(T).Name);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return Option<T>.None;
        }
    }

    /// <summary>
    /// Executes a stored procedure that returns paginated results.
    /// The stored procedure must accept <c>@PageNumber</c> and <c>@PageSize</c> parameters
    /// and return two result sets: the page items and a scalar total count.
    /// </summary>
    /// <typeparam name="T">The result item type.</typeparam>
    /// <param name="procName">The name of the stored procedure to execute.</param>
    /// <param name="param">Parameters to pass to the stored procedure (excluding pagination parameters).</param>
    /// <param name="pageNumber">The 1-based page number. Values less than 1 are clamped to 1.</param>
    /// <param name="pageSize">Items per page (1–1000). Values outside this range are clamped.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    /// <returns>A paginated result set or a failure when the Dapper backend is not configured.</returns>
    public async Task<Fin<QueryResults<T>>> ExecuteStoredProcPaginatedAsync<T>(
        string procName,
        object param,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (Connection is null)
        {
            return FinFail<QueryResults<T>>(Error.New("Dapper backend is not configured."));
        }

        if (string.IsNullOrWhiteSpace(procName))
        {
            return FinFail<QueryResults<T>>(Error.New("Stored procedure name cannot be empty."));
        }

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 1000);

        using var activity = StartDapperActivity("dapper.storedproc.paginated", procName);

        try
        {
            Logger?.LogDebug("Executing paginated stored procedure {ProcName} page {PageNumber} size {PageSize}", procName, pageNumber, pageSize);
            var result = await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(Connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            procName,
                            param,
                            transaction: Owner.AmbientTransaction,
                            commandTimeout: Options.CommandTimeoutSeconds,
                            commandType: CommandType.StoredProcedure,
                            cancellationToken: ct);

                        using var multi = await Connection.QueryMultipleAsync(command).ConfigureAwait(false);
                        var items = (await multi.ReadAsync<T>().ConfigureAwait(false)).ToList();
                        var totalCount = await multi.ReadSingleAsync<int>().ConfigureAwait(false);
                        return (items, totalCount);
                    },
                    cancellationToken,
                    procName)
                .ConfigureAwait(false);

            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return new QueryResults<T>(result.items.AsReadOnly(), result.totalCount, pageNumber, pageSize);
        }
        catch (Exception exception)
        {
            Logger?.LogError(exception, "Paginated stored procedure {ProcName} failed", procName);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return FinFail<QueryResults<T>>(Error.New(exception));
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        string operationName)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
                when (attempt < Options.MaxRetryCount && SqlTransientDetector.IsTransient(exception))
            {
                var retryDelay = Options.GetRetryDelay(attempt + 1);
                Logger?.LogWarning(exception, "Transient SQL failure on operation {OperationName} attempt {Attempt}. Retrying in {DelayMs} ms", operationName, attempt + 1, retryDelay.TotalMilliseconds);
                Activity.Current?.AddEvent(new ActivityEvent("retry", tags: new ActivityTagsCollection
                {
                    { SharpFunctionalMsSqlDiagnostics.RetryAttemptTag, attempt + 1 },
                    { "retry.delay.ms", retryDelay.TotalMilliseconds }
                }));
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static Activity? StartDapperActivity(string operation, string statement)
    {
        var activity = SharpFunctionalMsSqlDiagnostics.ActivitySource.StartActivity("sharpfunctional.mssql.dapper");
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.BackendTag, "dapper");
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.OperationTag, operation);
        activity?.SetTag("db.system", "mssql");
        activity?.SetTag("db.statement", statement);
        return activity;
    }

    private static async ValueTask EnsureOpenAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State == ConnectionState.Open)
        {
            return;
        }

        if (connection is not System.Data.Common.DbConnection dbConnection)
        {
            throw new InvalidOperationException("Connection must derive from DbConnection for async open operations.");
        }

        await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }
}
