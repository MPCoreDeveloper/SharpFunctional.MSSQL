using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharpFunctional.MsSql.Common;
using SharpFunctional.MsSql.DependencyInjection;
using Xunit;

namespace SharpFunctional.MsSql.Tests;

public class ServiceCollectionExtensionsTests
{
    // ── AddFunctionalMsSqlEf ─────────────────────────────────────────────

    [Fact]
    public void AddFunctionalMsSqlEf_WithNullServices_ShouldThrowArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddFunctionalMsSqlEf<TestDbContext>());
    }

    [Fact]
    public void AddFunctionalMsSqlEf_ShouldResolveFunctionalMsSqlDb()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(DatabaseFixture.ConnectionString));
        services.AddFunctionalMsSqlEf<TestDbContext>();

        // Act
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();

        // Assert
        Assert.NotNull(db);
    }

    [Fact]
    public void AddFunctionalMsSqlEf_ShouldRegisterAsScoped_SameInstancePerScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(DatabaseFixture.ConnectionString));
        services.AddFunctionalMsSqlEf<TestDbContext>();

        // Act
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db1 = scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();
        var db2 = scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();

        // Assert
        Assert.Same(db1, db2);
    }

    [Fact]
    public void AddFunctionalMsSqlEf_ShouldRegisterAsScoped_DifferentInstancesPerScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(DatabaseFixture.ConnectionString));
        services.AddFunctionalMsSqlEf<TestDbContext>();

        FunctionalMsSqlDb db1, db2;

        // Act
        using var provider = services.BuildServiceProvider();
        using (var scope1 = provider.CreateScope())
            db1 = scope1.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();
        using (var scope2 = provider.CreateScope())
            db2 = scope2.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();

        // Assert
        Assert.NotSame(db1, db2);
    }

    [Fact]
    public void AddFunctionalMsSqlEf_WithConfigure_ShouldApplyExecutionOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(DatabaseFixture.ConnectionString));
        services.AddFunctionalMsSqlEf<TestDbContext>(opts =>
            opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 60));

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FunctionalMsSqlDbOptions>>().Value;

        // Assert
        Assert.Equal(60, options.ExecutionOptions.CommandTimeoutSeconds);
    }

    // ── AddFunctionalMsSqlDapper ─────────────────────────────────────────

    [Fact]
    public void AddFunctionalMsSqlDapper_WithNullServices_ShouldThrowArgumentNullException()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(
            () => services.AddFunctionalMsSqlDapper(DatabaseFixture.ConnectionString));
    }

    [Fact]
    public void AddFunctionalMsSqlDapper_WithNullConnectionString_ShouldThrowArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddFunctionalMsSqlDapper(null!));
    }

    [Fact]
    public void AddFunctionalMsSqlDapper_WithWhitespaceConnectionString_ShouldThrowArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddFunctionalMsSqlDapper("   "));
    }

    [Fact]
    public void AddFunctionalMsSqlDapper_ShouldResolveFunctionalMsSqlDb()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFunctionalMsSqlDapper(DatabaseFixture.ConnectionString);

        // Act
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();

        // Assert
        Assert.NotNull(db);
    }

    [Fact]
    public void AddFunctionalMsSqlDapper_ShouldRegisterAsScoped_SameInstancePerScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFunctionalMsSqlDapper(DatabaseFixture.ConnectionString);

        // Act
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db1 = scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();
        var db2 = scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();

        // Assert
        Assert.Same(db1, db2);
    }

    [Fact]
    public void AddFunctionalMsSqlDapper_WithConfigure_ShouldApplyExecutionOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFunctionalMsSqlDapper(DatabaseFixture.ConnectionString, opts =>
            opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 45));

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FunctionalMsSqlDbOptions>>().Value;

        // Assert
        Assert.Equal(45, options.ExecutionOptions.CommandTimeoutSeconds);
    }

    [Fact]
    public void AddFunctionalMsSqlDapper_WithConfigure_ShouldPersistConnectionStringInOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddFunctionalMsSqlDapper(DatabaseFixture.ConnectionString);

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<FunctionalMsSqlDbOptions>>().Value;

        // Assert
        Assert.Equal(DatabaseFixture.ConnectionString, options.ConnectionString);
    }

    // ── AddFunctionalMsSql (combined, string overload) ───────────────────

    [Fact]
    public void AddFunctionalMsSql_WithConnectionString_ShouldResolveFunctionalMsSqlDb()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(DatabaseFixture.ConnectionString));
        services.AddFunctionalMsSql<TestDbContext>(DatabaseFixture.ConnectionString);

        // Act
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();

        // Assert
        Assert.NotNull(db);
    }

    [Fact]
    public void AddFunctionalMsSql_WithConnectionString_ShouldRegisterAsScoped_SameInstancePerScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(DatabaseFixture.ConnectionString));
        services.AddFunctionalMsSql<TestDbContext>(DatabaseFixture.ConnectionString);

        // Act
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db1 = scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();
        var db2 = scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();

        // Assert
        Assert.Same(db1, db2);
    }

    [Fact]
    public void AddFunctionalMsSql_WithNullConnectionString_ShouldThrowArgumentNullException()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(DatabaseFixture.ConnectionString));

        Assert.Throws<ArgumentNullException>(
            () => services.AddFunctionalMsSql<TestDbContext>(connectionString: null!));
    }

    // ── AddFunctionalMsSql (combined, configure delegate overload) ────────

    [Fact]
    public void AddFunctionalMsSql_WithConfigureDelegate_ShouldResolveFunctionalMsSqlDb()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(DatabaseFixture.ConnectionString));
        services.AddFunctionalMsSql<TestDbContext>(opts =>
            opts.ConnectionString = DatabaseFixture.ConnectionString);

        // Act
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();

        // Assert
        Assert.NotNull(db);
    }

    [Fact]
    public void AddFunctionalMsSql_WithConfigureDelegate_MissingConnectionString_ShouldThrowOnResolve()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(DatabaseFixture.ConnectionString));
        services.AddFunctionalMsSql<TestDbContext>(opts =>
        {
            // intentionally no ConnectionString set
        });

        // Act
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Assert
        Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>());
    }

    [Fact]
    public void AddFunctionalMsSql_WithNullConfigureDelegate_ShouldThrowArgumentNullException()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(DatabaseFixture.ConnectionString));

        Assert.Throws<ArgumentNullException>(
            () => services.AddFunctionalMsSql<TestDbContext>((Action<FunctionalMsSqlDbOptions>)null!));
    }

    // ── FunctionalMsSqlDbOptions defaults ────────────────────────────────

    [Fact]
    public void FunctionalMsSqlDbOptions_DefaultValues_ShouldMatchSqlExecutionOptionsDefault()
    {
        // Arrange
        var opts = new FunctionalMsSqlDbOptions();

        // Assert
        Assert.Null(opts.ConnectionString);
        Assert.Equal(SqlExecutionOptions.Default.CommandTimeoutSeconds, opts.ExecutionOptions.CommandTimeoutSeconds);
        Assert.Equal(SqlExecutionOptions.Default.MaxRetryCount, opts.ExecutionOptions.MaxRetryCount);
        Assert.Equal(SqlExecutionOptions.Default.BaseRetryDelay, opts.ExecutionOptions.BaseRetryDelay);
        Assert.Equal(SqlExecutionOptions.Default.MaxRetryDelay, opts.ExecutionOptions.MaxRetryDelay);
    }
}
