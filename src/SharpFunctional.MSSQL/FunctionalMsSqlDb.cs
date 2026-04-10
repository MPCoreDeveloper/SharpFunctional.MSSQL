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
/// Internal logging uses source-generated <c>LoggerMessage</c> methods to keep package diagnostics low-allocation for consumers.
///
/// EF example:
/// <code>
/// var db = new FunctionalMsSqlDb(dbContext: context);
/// var user = await db.Ef().GetByIdAsync&lt;User, int&gt;(42);
/// </code>
///
/// Dapper example:
/// <code>
/// var db = new FunctionalMsSqlDb(dbconnection: sqlConnection);
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
    IDbConnection? dbConnection = null,
    SqlExecutionOptions? executionOptions = null,
    ILogger<FunctionalMsSqlDb>? logger = null)
{
    private readonly bool _hasBackend = dbContext is not null || dbConnection is not null
        ? true
        : throw new ArgumentException("Either dbContext or dbConnection must be provided.");
    private IDbTransaction? _ambientTransaction;

    private SqlExecutionOptions Options => executionOptions ?? SqlExecutionOptions.Default;
    private ILogger<FunctionalMsSqlDb>? Log => logger;

    internal IDbTransaction? GetAmbientTransaction() => _ambientTransaction;

    /// <summary>
    /// Returns the EF Core functional accessor. Default behavior is no-tracking.
    /// </summary>
    public EfFunctionalDb Ef() => new(dbContext, executionOptions: Options);

    /// <summary>
    /// Returns the Dapper functional accessor.
    /// </summary>
    public DapperFunctionalDb Dapper() => new(dbConnection, this, Options, Log);

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
        if (!_hasBackend)
        {
            return FinFail<T>(Error.New("No backend is configured. Configure either DbContext or DbConnection."));
        }

        if (action is null)
        {
            return FinFail<T>(Error.New("Transaction action cannot be null."));
        }

        var loggerInstance = Log;
        var resultTypeName = typeof(T).Name;

        if (dbContext is not null)
        {
            using var activity = SharpFunctionalMsSqlDiagnostics.ActivitySource.StartActivity("sharpfunctional.mssql.transaction");
            SharpFunctionalMsSqlDiagnostics.ApplyActivityEnricher(activity, Options);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.BackendTag, "ef");
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.OperationTag, nameof(InTransactionAsync));
            activity?.SetTag("db.system", "mssql");

            try
            {
                loggerInstance?.StartingEfTransaction(resultTypeName);

                await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var result = await action(this).ConfigureAwait(false);

                if (result.IsSucc)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    loggerInstance?.CommittedEfTransaction(resultTypeName);

                    activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return result;
                }

                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                loggerInstance?.RolledBackEfTransaction(resultTypeName);

                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                activity?.SetStatus(ActivityStatusCode.Error, "transaction result failed");
                return result;
            }
            catch (Exception exception)
            {
                loggerInstance?.EfTransactionFailed(resultTypeName, exception);

                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                return FinFail<T>(Error.New(exception));
            }
        }

        if (dbConnection is null)
        {
            return FinFail<T>(Error.New("No backend is configured. Configure either DbContext or IDbConnection."));
        }

        if (_ambientTransaction is not null)
        {
            return FinFail<T>(Error.New("Nested transactions are not supported for Dapper backend."));
        }

        using var dapperActivity = SharpFunctionalMsSqlDiagnostics.ActivitySource.StartActivity("sharpfunctional.mssql.transaction");
        SharpFunctionalMsSqlDiagnostics.ApplyActivityEnricher(dapperActivity, Options);
        dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.BackendTag, "dapper");
        dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.OperationTag, nameof(InTransactionAsync));
        dapperActivity?.SetTag("db.system", "mssql");

        try
        {
            loggerInstance?.StartingDapperTransaction(resultTypeName);

            if (dbConnection.State != ConnectionState.Open)
            {
                if (dbConnection is not System.Data.Common.DbConnection dbConn)
                {
                    dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                    dapperActivity?.SetStatus(ActivityStatusCode.Error, "dbConnection does not derive from DbConnection");
                    return FinFail<T>(Error.New("DbConnection must derive from DbConnection for async open operations."));
                }

                var openResult = await OpenConnectionWithRetryAsync(dbConn, cancellationToken).ConfigureAwait(false);
                if (openResult.IsFail)
                {
                    dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                    dapperActivity?.SetStatus(ActivityStatusCode.Error, "dbConnection open failed");
                    return openResult.Match(
                        Succ: _ => FinFail<T>(Error.New("Failed to open SQL dbConnection.")),
                        Fail: error => FinFail<T>(error));
                }
            }

            _ambientTransaction = dbConnection.BeginTransaction();

            var result = await action(this).ConfigureAwait(false);
            if (result.IsSucc)
            {
                _ambientTransaction.Commit();
                loggerInstance?.CommittedDapperTransaction(resultTypeName);

                dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
                dapperActivity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }

            _ambientTransaction.Rollback();
            loggerInstance?.RolledBackDapperTransaction(resultTypeName);

            dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            dapperActivity?.SetStatus(ActivityStatusCode.Error, "transaction result failed");
            return result;
        }
        catch (Exception exception)
        {
            try { _ambientTransaction?.Rollback(); } catch { }

            loggerInstance?.DapperTransactionFailed(resultTypeName, exception);

            dapperActivity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            dapperActivity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return FinFail<T>(Error.New(exception));
        }
        finally
        {
            _ambientTransaction?.Dispose();
            _ambientTransaction = null;
        }
    }

    private async Task<Fin<Unit>> OpenConnectionWithRetryAsync(
        System.Data.Common.DbConnection dbConnection,
        CancellationToken cancellationToken)
    {
        using var activity = SharpFunctionalMsSqlDiagnostics.ActivitySource.StartActivity("sharpfunctional.mssql.dbconnection.open");
        SharpFunctionalMsSqlDiagnostics.ApplyActivityEnricher(activity, Options);
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.BackendTag, "dapper");
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.OperationTag, "dbconnection.open");
        activity?.SetTag("db.system", "mssql");

        var loggerInstance = Log;
        var attempt = 0;

        while (true)
        {
            try
            {
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                loggerInstance?.OpenedSqlConnection(attempt + 1);

                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.RetryAttemptTag, attempt + 1);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return unit;
            }
            catch (Exception exception)
                when (attempt < Options.MaxRetryCount && SqlTransientDetector.IsTransient(exception))
            {
                var retryDelay = Options.GetRetryDelay(attempt + 1);
                loggerInstance?.TransientSqlOpenFailure(attempt + 1, retryDelay.TotalMilliseconds, exception);

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
                loggerInstance?.SqlConnectionOpenFailed(attempt + 1, exception);

                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.RetryAttemptTag, attempt + 1);
                activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                return FinFail<Unit>(Error.New(exception));
            }
        }
    }
}
