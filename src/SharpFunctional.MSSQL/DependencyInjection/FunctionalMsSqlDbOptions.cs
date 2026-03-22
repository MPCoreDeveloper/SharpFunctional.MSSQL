using SharpFunctional.MsSql.Common;

namespace SharpFunctional.MsSql.DependencyInjection;

/// <summary>
/// Configuration options for registering <see cref="SharpFunctional.MsSql.FunctionalMsSqlDb"/> via dependency injection.
/// </summary>
public sealed class FunctionalMsSqlDbOptions
{
    /// <summary>
    /// Connection string used by the Dapper backend.
    /// Required when registering with <see cref="ServiceCollectionExtensions.AddFunctionalMsSqlDapper"/>
    /// or <see cref="ServiceCollectionExtensions.AddFunctionalMsSql{TContext}(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{FunctionalMsSqlDbOptions})"/>.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// SQL command timeout and retry policy applied to all Dapper operations.
    /// Defaults to <see cref="SqlExecutionOptions.Default"/>.
    /// </summary>
    public SqlExecutionOptions ExecutionOptions { get; set; } = SqlExecutionOptions.Default;
}
