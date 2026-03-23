using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharpFunctional.MsSql.Common;
using SharpFunctional.MsSql.Dapper;
using SharpFunctional.MsSql.Ef;
using SharpFunctional.MsSql.Functional;
using static SharpFunctional.MsSql.Functional.Prelude;

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
    private DbContext? EfContext => dbContext;
    private IDbConnection? ConnectionDb => connection;
    private SqlExecutionOptions Options => executionOptions ?? SqlExecutionOptions.Default;
    private ILogger<FunctionalMsSqlDb>? Log => logger;

    internal IDbTransaction? AmbientTransaction { get; private set; }

    /// <summary>
    /// Returns the EF Core functional accessor. Default behavior is no-tracking.
    /// </summary>
    public EfFunctionalDb Ef() => new(EfContext);

    /// <summary>
    /// Returns the Dapper functional accessor.
    /// </summary>
    public DapperFunctionalDb Dapper() => new(ConnectionDb, this, Options, Log);

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

        if (EfContext is not null)
        {
            using var activity = SharpFunctionalMsSqlDiagnostics.ActivitySource.StartActivity("sharpfunctional.mssql.transaction");
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.BackendTag, "ef");
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.OperationTag, nameof(InTransactionAsync));
            activity?.SetTag("db.system", "mssql");

            try
            {
                if (Log?.IsEnabled(LogLevel.Debug) is true)
                {
                    Log.LogDebug("Starting EF transaction for result type {ResultType}", typeof(T).Name);
                }

                await using var transaction = await EfContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var result = await action(this).ConfigureAwait(false);

                if (result.IsSucc)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    if (Log?.IsEnabled(LogLevel.Debug) is true)
                    {
                        Log.LogDebug("Committed EF transaction for result type {ResultType}", typeof(T).Name);
                    }

                    activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return result;
                }

                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                Log?.LogWarning("Rolled back EF transaction due to failed result for type {ResultType}", typeof(T).Name);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                activity?.SetStatus(ActivityStatusCode.Error, "transaction result failed");
                return result;
            }
            catch (Exception exception)
            {
                Log?.LogError(exception, "EF transaction failed for result type {ResultType}", typeof(T).Name);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                return FinFail<T>(Error.New(exception));
            }
        }

        if (ConnectionDb is null)
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
            if (Log?.IsEnabled(LogLevel.Debug) is true)
            {
                Log.LogDebug("Starting Dapper transaction for result type {ResultType}", typeof(T).Name);
            }

            if (ConnectionDb.State != ConnectionState.Open)
            {
                if (ConnectionDb is not System.Data.Common.DbConnection dbConnection)
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

            using var transaction = ConnectionDb.BeginTransaction();
            AmbientTransaction = transaction;

            var result = await action(this).ConfigureAwait(false);
            if (result.IsSucc)
            {
                transaction.Commit();
                if (Log?.IsEnabled(LogLevel.Debug) is true)
                {
                    Log.LogDebug("Committed Dapper transaction for result type {ResultType}", typeof(T).Name);
                }

                dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
                dapperActivity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }

            transaction.Rollback();
            Log?.LogWarning("Rolled back Dapper transaction due to failed result for type {ResultType}", typeof(T).Name);
            dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            dapperActivity?.SetStatus(ActivityStatusCode.Error, "transaction result failed");
            return result;
        }
        catch (Exception exception)
        {
            Log?.LogError(exception, "Dapper transaction failed for result type {ResultType}", typeof(T).Name);
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

        var attempt = 0;

        while (true)
        {
            try
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                if (Log?.IsEnabled(LogLevel.Debug) is true)
                {
                    Log.LogDebug("Opened SQL connection after {AttemptCount} attempt(s)", attempt + 1);
                }

                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.RetryAttemptTag, attempt + 1);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return unit;
            }
            catch (Exception exception)
                when (attempt < Options.MaxRetryCount && SqlTransientDetector.IsTransient(exception))
            {
                var retryDelay = Options.GetRetryDelay(attempt + 1);
                Log?.LogWarning(exception, "Transient SQL open failure on attempt {Attempt}. Retrying in {DelayMs} ms", attempt + 1, retryDelay.TotalMilliseconds);
                activity?.AddEvent(new ActivityEvent("retry", tags: new ActivityTagsCollection
                {
                    { SharpFunctionalMsSqlDiagnostics.RetryAttemptTag, attempt + 1 },
                    { "retry.delay.ms", retryDelay.TotalMilliseconds }
                }));
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                attempt++;
            }
            catch (Exception exception)
            {
                Log?.LogError(exception, "SQL connection open failed after {AttemptCount} attempt(s)", attempt + 1);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.RetryAttemptTag, attempt + 1);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                return FinFail<Unit>(Error.New(exception));
            }
        }
    }
}
