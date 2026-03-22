using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SharpFunctional.MsSql.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="FunctionalMsSqlDb"/> in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FunctionalMsSqlDb"/> with the EF Core backend only.
    /// The <typeparamref name="TContext"/> must already be registered in the container.
    /// </summary>
    /// <typeparam name="TContext">The <see cref="DbContext"/> type resolved from DI.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional delegate to configure <see cref="FunctionalMsSqlDbOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;(opts => opts.UseSqlServer(connectionString));
    /// services.AddFunctionalMsSqlEf&lt;AppDbContext&gt;(opts =>
    /// {
    ///     opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 60);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddFunctionalMsSqlEf<TContext>(
        this IServiceCollection services,
        Action<FunctionalMsSqlDbOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddScoped<FunctionalMsSqlDb>(sp =>
        {
            var context = sp.GetRequiredService<TContext>();
            var opts = sp.GetService<IOptions<FunctionalMsSqlDbOptions>>()?.Value ?? new FunctionalMsSqlDbOptions();
            var logger = sp.GetService<ILogger<FunctionalMsSqlDb>>();
            return new FunctionalMsSqlDb(dbContext: context, executionOptions: opts.ExecutionOptions, logger: logger);
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="FunctionalMsSqlDb"/> with the Dapper backend only.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="configure">Optional delegate to further configure <see cref="FunctionalMsSqlDbOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddFunctionalMsSqlDapper(connectionString, opts =>
    /// {
    ///     opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 30, maxRetryCount: 3);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddFunctionalMsSqlDapper(
        this IServiceCollection services,
        string connectionString,
        Action<FunctionalMsSqlDbOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.Configure<FunctionalMsSqlDbOptions>(opts => opts.ConnectionString = connectionString);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddScoped<FunctionalMsSqlDb>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<FunctionalMsSqlDbOptions>>().Value;
            var connection = new SqlConnection(opts.ConnectionString);
            var logger = sp.GetService<ILogger<FunctionalMsSqlDb>>();
            return new FunctionalMsSqlDb(connection: connection, executionOptions: opts.ExecutionOptions, logger: logger);
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="FunctionalMsSqlDb"/> with both the EF Core and Dapper backends.
    /// The <typeparamref name="TContext"/> must already be registered in the container.
    /// </summary>
    /// <typeparam name="TContext">The <see cref="DbContext"/> type resolved from DI.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQL Server connection string for the Dapper backend.</param>
    /// <param name="configure">Optional delegate to further configure <see cref="FunctionalMsSqlDbOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;(opts => opts.UseSqlServer(connectionString));
    /// services.AddFunctionalMsSql&lt;AppDbContext&gt;(connectionString, opts =>
    /// {
    ///     opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 60, maxRetryCount: 3);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddFunctionalMsSql<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<FunctionalMsSqlDbOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.Configure<FunctionalMsSqlDbOptions>(opts => opts.ConnectionString = connectionString);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddScoped<FunctionalMsSqlDb>(sp =>
        {
            var context = sp.GetRequiredService<TContext>();
            var opts = sp.GetRequiredService<IOptions<FunctionalMsSqlDbOptions>>().Value;
            var connection = new SqlConnection(opts.ConnectionString);
            var logger = sp.GetService<ILogger<FunctionalMsSqlDb>>();
            return new FunctionalMsSqlDb(dbContext: context, connection: connection, executionOptions: opts.ExecutionOptions, logger: logger);
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="FunctionalMsSqlDb"/> with both the EF Core and Dapper backends,
    /// resolving the connection string from <see cref="FunctionalMsSqlDbOptions.ConnectionString"/>.
    /// The <typeparamref name="TContext"/> must already be registered in the container.
    /// </summary>
    /// <typeparam name="TContext">The <see cref="DbContext"/> type resolved from DI.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate to configure <see cref="FunctionalMsSqlDbOptions"/> including the connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;(opts => opts.UseSqlServer(connectionString));
    /// services.AddFunctionalMsSql&lt;AppDbContext&gt;(opts =>
    /// {
    ///     opts.ConnectionString = connectionString;
    ///     opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 60);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddFunctionalMsSql<TContext>(
        this IServiceCollection services,
        Action<FunctionalMsSqlDbOptions> configure)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        services.AddScoped<FunctionalMsSqlDb>(sp =>
        {
            var context = sp.GetRequiredService<TContext>();
            var opts = sp.GetRequiredService<IOptions<FunctionalMsSqlDbOptions>>().Value;

            if (string.IsNullOrWhiteSpace(opts.ConnectionString))
            {
                throw new InvalidOperationException(
                    $"{nameof(FunctionalMsSqlDbOptions)}.{nameof(FunctionalMsSqlDbOptions.ConnectionString)} must be set when using the combined EF + Dapper backend.");
            }

            var connection = new SqlConnection(opts.ConnectionString);
            var logger = sp.GetService<ILogger<FunctionalMsSqlDb>>();
            return new FunctionalMsSqlDb(dbContext: context, connection: connection, executionOptions: opts.ExecutionOptions, logger: logger);
        });

        return services;
    }
}
