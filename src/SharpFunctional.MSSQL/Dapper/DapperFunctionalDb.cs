using System.Data;
using System.Diagnostics;
using LanguageExt;
using LanguageExt.Common;
using global::Dapper;
using Microsoft.Extensions.Logging;
using SharpFunctional.MsSql.Common;
using static LanguageExt.Prelude;

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
    private readonly IDbConnection? _connection = connection;
    private readonly FunctionalMsSqlDb _owner = owner;
    private readonly SqlExecutionOptions _executionOptions = executionOptions ?? SqlExecutionOptions.Default;
    private readonly ILogger? _logger = logger;

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
        if (_connection is null || string.IsNullOrWhiteSpace(procName))
        {
            return Option<T>.None;
        }

        using var activity = StartDapperActivity("dapper.storedproc.single", procName);

        try
        {
            _logger?.LogDebug("Executing stored procedure {ProcName} for single value type {ResultType}", procName, typeof(T).Name);
            var result = await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(_connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            procName,
                            param,
                            transaction: _owner.AmbientTransaction,
                            commandTimeout: _executionOptions.CommandTimeoutSeconds,
                            commandType: CommandType.StoredProcedure,
                            cancellationToken: ct);

                        return await _connection.QueryFirstOrDefaultAsync<T>(command).ConfigureAwait(false);
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
            _logger?.LogError(exception, "Stored procedure {ProcName} failed for single value type {ResultType}", procName, typeof(T).Name);
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
        if (_connection is null || string.IsNullOrWhiteSpace(procName))
        {
            return Seq<T>();
        }

        using var activity = StartDapperActivity("dapper.storedproc.seq", procName);

        try
        {
            _logger?.LogDebug("Executing stored procedure {ProcName} for sequence type {ResultType}", procName, typeof(T).Name);
            var result = await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(_connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            procName,
                            param,
                            transaction: _owner.AmbientTransaction,
                            commandTimeout: _executionOptions.CommandTimeoutSeconds,
                            commandType: CommandType.StoredProcedure,
                            cancellationToken: ct);

                        return await _connection.QueryAsync<T>(command).ConfigureAwait(false);
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
            _logger?.LogError(exception, "Stored procedure {ProcName} failed for sequence type {ResultType}", procName, typeof(T).Name);
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
        if (_connection is null)
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
            _logger?.LogDebug("Executing non-query stored procedure {ProcName}", procName);
            await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(_connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            procName,
                            param,
                            transaction: _owner.AmbientTransaction,
                            commandTimeout: _executionOptions.CommandTimeoutSeconds,
                            commandType: CommandType.StoredProcedure,
                            cancellationToken: ct);

                        _ = await _connection.ExecuteAsync(command).ConfigureAwait(false);
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
            _logger?.LogError(exception, "Non-query stored procedure {ProcName} failed", procName);
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
        if (_connection is null || string.IsNullOrWhiteSpace(sql))
        {
            return Seq<T>();
        }

        using var activity = StartDapperActivity("dapper.query.seq", sql);

        try
        {
            _logger?.LogDebug("Executing SQL query for sequence type {ResultType}", typeof(T).Name);
            var result = await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(_connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            sql,
                            param,
                            transaction: _owner.AmbientTransaction,
                            commandTimeout: _executionOptions.CommandTimeoutSeconds,
                            cancellationToken: ct);

                        return await _connection.QueryAsync<T>(command).ConfigureAwait(false);
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
            _logger?.LogError(exception, "SQL query failed for sequence type {ResultType}", typeof(T).Name);
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
        if (_connection is null || string.IsNullOrWhiteSpace(sql))
        {
            return Option<T>.None;
        }

        using var activity = StartDapperActivity("dapper.query.single", sql);

        try
        {
            _logger?.LogDebug("Executing SQL query for single value type {ResultType}", typeof(T).Name);
            var result = await ExecuteWithRetryAsync(
                    async ct =>
                    {
                        await EnsureOpenAsync(_connection, ct).ConfigureAwait(false);
                        var command = new CommandDefinition(
                            sql,
                            param,
                            transaction: _owner.AmbientTransaction,
                            commandTimeout: _executionOptions.CommandTimeoutSeconds,
                            cancellationToken: ct);

                        return await _connection.QueryFirstOrDefaultAsync<T>(command).ConfigureAwait(false);
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
            _logger?.LogError(exception, "SQL query failed for single value type {ResultType}", typeof(T).Name);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return Option<T>.None;
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
                when (attempt < _executionOptions.MaxRetryCount && SqlTransientDetector.IsTransient(exception))
            {
                var retryDelay = _executionOptions.GetRetryDelay(attempt + 1);
                _logger?.LogWarning(exception, "Transient SQL failure on operation {OperationName} attempt {Attempt}. Retrying in {DelayMs} ms", operationName, attempt + 1, retryDelay.TotalMilliseconds);
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
