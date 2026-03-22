using System.Data;
using System.Diagnostics;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharpFunctional.MsSql.Common;
using SharpFunctional.MsSql.Dapper;
using SharpFunctional.MsSql.Ef;
using static LanguageExt.Prelude;

namespace SharpFunctional.MsSql;

/// <summary>
/// Provides a functional-first entry point for SQL Server data access using either Entity Framework Core or Dapper.
/// </summary>
/// <remarks>
/// Thread safety: <see cref="DbContext"/> is not thread-safe. Use one <see cref="FunctionalMsSqlDb"/> instance per scope/request.
/// Public APIs never throw and map failures to <see cref="Fin{T}"/>.
///
/// EF example:
/// <code>
/// var db = new FunctionalMsSqlDb(dbContext: context);
/// var user = await db.Ef().GetByIdAsync&lt;User, int&gt;(42);
/// </code>
///
/// Dapper example:
/// <code>
/// var db = new FunctionalMsSqlDb(connection: sqlConnection);
/// var rows = await db.Dapper().ExecuteStoredProcAsync&lt;UserDto&gt;("dbo.Users_GetByStatus", new { Status = 1 });
/// </code>
///
/// Transaction example:
/// <code>
/// var result = await db.InTransactionAsync(async txDb =&gt;
/// {
///     var add = await txDb.Ef().AddAsync(new User { Name = "Ada" });
///     if (add.IsFail) return FinFail&lt;Unit&gt;(add.Error);
///
///     return await txDb.Ef().SaveAsync(new User { Name = "Ada" });
/// });
/// </code>
///
/// Functional chaining example:
/// <code>
/// var dtoSeq = await db.Ef()
///     .GetByIdAsync&lt;User, int&gt;(42)
///     .Bind(user =&gt; db.Ef().QueryAsync&lt;Order&gt;(o =&gt; o.UserId == user.Id))
///     .Map(orders =&gt; orders.Map(o =&gt; o.ToDto()));
/// </code>
/// </remarks>
public sealed class FunctionalMsSqlDb(
    DbContext? dbContext = null,
    IDbConnection? connection = null,
    SqlExecutionOptions? executionOptions = null,
    ILogger<FunctionalMsSqlDb>? logger = null)
{
    private readonly DbContext? _ef = dbContext;
    private readonly IDbConnection? _connection = connection;
    private readonly SqlExecutionOptions _executionOptions = executionOptions ?? SqlExecutionOptions.Default;
    private readonly ILogger<FunctionalMsSqlDb>? _logger = logger;

    internal IDbTransaction? AmbientTransaction { get; private set; }

    /// <summary>
    /// Returns the EF Core functional accessor. Default behavior is no-tracking.
    /// </summary>
    public EfFunctionalDb Ef() => new(_ef);

    /// <summary>
    /// Returns the Dapper functional accessor.
    /// </summary>
    public DapperFunctionalDb Dapper() => new(_connection, this, _executionOptions, _logger);

    /// <summary>
    /// Executes an action in a transaction and commits only when the action succeeds.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="action">Transactional action to execute.</param>
    /// <param name="cancellationToken">Token used to cancel the transaction flow.</param>
    /// <returns>A successful value when committed; otherwise a failure.</returns>
    public async Task<Fin<T>> InTransactionAsync<T>(
        Func<FunctionalMsSqlDb, Task<Fin<T>>> action,
        CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            return FinFail<T>(Error.New("Transaction action cannot be null."));
        }

        if (_ef is not null)
        {
            using var activity = SharpFunctionalMsSqlDiagnostics.ActivitySource.StartActivity("sharpfunctional.mssql.transaction");
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.BackendTag, "ef");
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.OperationTag, nameof(InTransactionAsync));
            activity?.SetTag("db.system", "mssql");

            try
            {
                _logger?.LogDebug("Starting EF transaction for result type {ResultType}", typeof(T).Name);
                await using var transaction = await _ef.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var result = await action(this).ConfigureAwait(false);

                if (result.IsSucc)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    _logger?.LogDebug("Committed EF transaction for result type {ResultType}", typeof(T).Name);
                    activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return result;
                }

                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _logger?.LogWarning("Rolled back EF transaction due to failed result for type {ResultType}", typeof(T).Name);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                activity?.SetStatus(ActivityStatusCode.Error, "transaction result failed");
                return result;
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "EF transaction failed for result type {ResultType}", typeof(T).Name);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                return FinFail<T>(Error.New(exception));
            }
        }

        if (_connection is null)
        {
            return FinFail<T>(Error.New("No backend is configured. Configure either DbContext or IDbConnection."));
        }

        if (AmbientTransaction is not null)
        {
            return FinFail<T>(Error.New("Nested transactions are not supported for Dapper backend."));
        }

        using var dapperActivity = SharpFunctionalMsSqlDiagnostics.ActivitySource.StartActivity("sharpfunctional.mssql.transaction");
        dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.BackendTag, "dapper");
        dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.OperationTag, nameof(InTransactionAsync));
        dapperActivity?.SetTag("db.system", "mssql");

        try
        {
            _logger?.LogDebug("Starting Dapper transaction for result type {ResultType}", typeof(T).Name);
            if (_connection.State != ConnectionState.Open)
            {
                if (_connection is not System.Data.Common.DbConnection dbConnection)
                {
                    dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                    dapperActivity?.SetStatus(ActivityStatusCode.Error, "connection does not derive from DbConnection");
                    return FinFail<T>(Error.New("Connection must derive from DbConnection for async open operations."));
                }

                var openResult = await OpenConnectionWithRetryAsync(dbConnection, cancellationToken).ConfigureAwait(false);
                if (openResult.IsFail)
                {
                    dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                    dapperActivity?.SetStatus(ActivityStatusCode.Error, "connection open failed");
                    return openResult.Match(
                        Succ: _ => FinFail<T>(Error.New("Failed to open SQL connection.")),
                        Fail: error => FinFail<T>(error));
                }
            }

            using var transaction = _connection.BeginTransaction();
            AmbientTransaction = transaction;

            var result = await action(this).ConfigureAwait(false);
            if (result.IsSucc)
            {
                transaction.Commit();
                _logger?.LogDebug("Committed Dapper transaction for result type {ResultType}", typeof(T).Name);
                dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
                dapperActivity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }

            transaction.Rollback();
            _logger?.LogWarning("Rolled back Dapper transaction due to failed result for type {ResultType}", typeof(T).Name);
            dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            dapperActivity?.SetStatus(ActivityStatusCode.Error, "transaction result failed");
            return result;
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Dapper transaction failed for result type {ResultType}", typeof(T).Name);
            dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            dapperActivity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return FinFail<T>(Error.New(exception));
        }
        finally
        {
            AmbientTransaction = null;
        }
    }

    private async Task<Fin<Unit>> OpenConnectionWithRetryAsync(
        System.Data.Common.DbConnection dbConnection,
        CancellationToken cancellationToken)
    {
        using var activity = SharpFunctionalMsSqlDiagnostics.ActivitySource.StartActivity("sharpfunctional.mssql.connection.open");
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.BackendTag, "dapper");
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.OperationTag, "connection.open");
        activity?.SetTag("db.system", "mssql");

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                _logger?.LogDebug("Opened SQL connection after {AttemptCount} attempt(s)", attempt + 1);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.RetryAttemptTag, attempt + 1);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return unit;
            }
            catch (Exception exception)
                when (attempt < _executionOptions.MaxRetryCount && SqlTransientDetector.IsTransient(exception))
            {
                var retryDelay = _executionOptions.GetRetryDelay(attempt + 1);
                _logger?.LogWarning(exception, "Transient SQL open failure on attempt {Attempt}. Retrying in {DelayMs} ms", attempt + 1, retryDelay.TotalMilliseconds);
                activity?.AddEvent(new ActivityEvent("retry", tags: new ActivityTagsCollection
                {
                    { SharpFunctionalMsSqlDiagnostics.RetryAttemptTag, attempt + 1 },
                    { "retry.delay.ms", retryDelay.TotalMilliseconds }
                }));
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "SQL connection open failed after {AttemptCount} attempt(s)", attempt + 1);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.RetryAttemptTag, attempt + 1);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                return FinFail<Unit>(Error.New(exception));
            }
        }
    }
}
