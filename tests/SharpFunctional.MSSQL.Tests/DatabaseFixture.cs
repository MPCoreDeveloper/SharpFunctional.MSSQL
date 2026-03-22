using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SharpFunctional.MsSql.Tests;

/// <summary>
/// Shared database fixture that provisions a clean MSSQL LocalDB database once per test run.
/// All test classes that use <c>[Collection(DatabaseFixture.CollectionName)]</c> share this
/// instance and run sequentially, avoiding parallel schema conflicts.
/// <para>
/// <strong>Prerequisite:</strong> A running SQL Server LocalDB instance at
/// <c>(localdb)\MSSQLLocalDB</c>. The <c>TestDB</c> database is recreated automatically.
/// </para>
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    /// <summary>
    /// xUnit collection name used by all database-dependent test classes.
    /// </summary>
    public const string CollectionName = "Database";

    /// <summary>
    /// Connection string targeting SQL Server LocalDB.
    /// Requires a running <c>(localdb)\MSSQLLocalDB</c> instance.
    /// </summary>
    public const string ConnectionString =
        "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=TestDB;Integrated Security=True;" +
        "Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;" +
        "Encrypt=True;TrustServerCertificate=False;Command Timeout=30";

    /// <summary>
    /// Creates <see cref="DbContextOptions{TContext}"/> configured for SQL Server LocalDB.
    /// </summary>
    public static DbContextOptions<TestDbContext> CreateDbContextOptions() =>
        new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

    /// <summary>
    /// Creates a new open <see cref="SqlConnection"/> to the test database.
    /// Caller is responsible for disposing.
    /// </summary>
    public static SqlConnection CreateOpenConnection()
    {
        var connection = new SqlConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Creates a new <see cref="TestDbContext"/> against the test database.
    /// Caller is responsible for disposing.
    /// </summary>
    public TestDbContext CreateDbContext() => new(CreateDbContextOptions());

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Defines the xUnit test collection that shares a single <see cref="DatabaseFixture"/>.
/// </summary>
[CollectionDefinition(DatabaseFixture.CollectionName)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;

