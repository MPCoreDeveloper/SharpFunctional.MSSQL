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
}
