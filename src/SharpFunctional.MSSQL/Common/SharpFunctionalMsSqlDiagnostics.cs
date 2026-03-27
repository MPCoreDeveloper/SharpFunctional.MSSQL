using System.Diagnostics;

namespace SharpFunctional.MsSql.Common;

/// <summary>
/// Provides OpenTelemetry diagnostics primitives for SharpFunctional.MSSQL.
/// </summary>
public static class SharpFunctionalMsSqlDiagnostics
{
    /// <summary>
    /// Activity source used for tracing SharpFunctional.MSSQL operations.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("SharpFunctional.MsSql");

    /// <summary>Tag key for the storage backend.</summary>
    public const string BackendTag = "sharpfunctional.mssql.backend";

    /// <summary>Tag key for operation name.</summary>
    public const string OperationTag = "sharpfunctional.mssql.operation";

    /// <summary>Tag key indicating whether the operation succeeded.</summary>
    public const string SuccessTag = "sharpfunctional.mssql.success";

    /// <summary>Tag key for retry attempt count.</summary>
    public const string RetryAttemptTag = "sharpfunctional.mssql.retry.attempt";

    /// <summary>Tag key for the correlation identifier that links related operations.</summary>
    public const string CorrelationIdTag = "sharpfunctional.mssql.correlation_id";

    /// <summary>Tag key for operation duration in milliseconds.</summary>
    public const string DurationMsTag = "sharpfunctional.mssql.duration_ms";

    /// <summary>Tag key for the entity type involved in the operation.</summary>
    public const string EntityTypeTag = "sharpfunctional.mssql.entity_type";

    /// <summary>Tag key for the batch size of bulk operations.</summary>
    public const string BatchSizeTag = "sharpfunctional.mssql.batch_size";

    /// <summary>Tag key for the number of items affected by the operation.</summary>
    public const string ItemCountTag = "sharpfunctional.mssql.item_count";

    /// <summary>Tag key for the page number in paginated queries.</summary>
    public const string PageNumberTag = "sharpfunctional.mssql.page_number";

    /// <summary>Tag key for the page size in paginated queries.</summary>
    public const string PageSizeTag = "sharpfunctional.mssql.page_size";

    /// <summary>Tag key for the circuit breaker state.</summary>
    public const string CircuitStateTag = "sharpfunctional.mssql.circuit_state";

    /// <summary>
    /// Applies an optional activity enricher delegate and ignores enricher failures
    /// so diagnostics customization cannot break data access execution.
    /// </summary>
    /// <param name="activity">Activity to enrich.</param>
    /// <param name="options">Execution options that may contain an enricher delegate.</param>
    public static void ApplyActivityEnricher(Activity? activity, SqlExecutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (activity is null || options.ActivityEnricher is null)
        {
            return;
        }

        try
        {
            options.ActivityEnricher(activity);
        }
        catch
        {
            // Intentionally ignored to keep telemetry customization non-disruptive.
        }
    }
}
