using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;
using Xunit;

namespace SharpFunctional.MsSql.Tests;

/// <summary>
/// Shared database fixture that provisions a clean MSSQL database once per test run.
/// Automatically selects between LocalDB (Windows) and Docker SQL Server (Linux/containers).
/// All test classes that use <c>[Collection(DatabaseFixture.CollectionName)]</c> share this
/// instance and run sequentially, avoiding parallel schema conflicts.
/// <para>
/// <strong>Windows:</strong> Uses SQL Server LocalDB at <c>(localdb)\MSSQLLocalDB</c>.
/// <br />
/// <strong>Linux/macOS:</strong> Connects to Docker SQL Server at <c>localhost:1433</c>.
/// Set environment variables to override:
/// <list type="bullet">
/// <item><c>TEST_DB_SERVER</c> — Server/host name (default: localhost or (localdb)\MSSQLLocalDB)</item>
/// <item><c>TEST_DB_PORT</c> — Port number (default: 1433 for Docker, unused for LocalDB)</item>
/// <item><c>TEST_DB_USER</c> — Username (default: sa for Docker, uses Integrated Security for LocalDB)</item>
/// <item><c>TEST_DB_PASSWORD</c> — Password (default: YourPassword123! for Docker)</item>
/// </list>
/// </para>
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    /// <summary>
    /// xUnit collection name used by all database-dependent test classes.
    /// </summary>
    public const string CollectionName = "Database";

    private static readonly bool IsWindowsOs = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Connection string resolved based on platform.
    /// </summary>
    public static string ConnectionString { get; } = ResolveConnectionString();

    /// <summary>
    /// Resolves the appropriate connection string based on platform and environment.
    /// </summary>
    private static string ResolveConnectionString()
    {
        var server = Environment.GetEnvironmentVariable("TEST_DB_SERVER");
        var port = Environment.GetEnvironmentVariable("TEST_DB_PORT");
        var user = Environment.GetEnvironmentVariable("TEST_DB_USER");
        var password = Environment.GetEnvironmentVariable("TEST_DB_PASSWORD");

        if (IsWindowsOs && string.IsNullOrEmpty(server))
        {
            // Windows: Use LocalDB with Integrated Security
            return "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=TestDB;Integrated Security=True;" +
                   "Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;" +
                   "Encrypt=True;TrustServerCertificate=False;Command Timeout=30";
        }

        // Linux/macOS or explicit override: Use Docker SQL Server (or explicit config)
        var host = server ?? "localhost";
        var portStr = port ?? "1433";
        var username = user ?? "sa";
        var pwd = password ?? "YourPassword123!";

        return $"Server={host},{portStr};Database=TestDB;User Id={username};Password={pwd};" +
               "Encrypt=True;TrustServerCertificate=True;Connection Timeout=30";
    }

    /// <summary>
    /// Creates <see cref="DbContextOptions{TContext}"/> configured for the current platform's SQL Server.
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
        try
        {
            await using var context = CreateDbContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
        catch (SqlException ex) when (ex.Number == -2 || ex.Number == 4060)
        {
            // -2: Connection timeout or login failed
            // 4060: Cannot open database
            var platform = IsWindowsOs ? "Windows (LocalDB)" : "Linux/macOS (Docker SQL Server)";
            throw new InvalidOperationException(
                $"Failed to connect to test database on {platform}.\n" +
                $"Connection string: {ConnectionString}\n" +
                $"Environment variables:\n" +
                $"  TEST_DB_SERVER={Environment.GetEnvironmentVariable("TEST_DB_SERVER") ?? "(not set)"}\n" +
                $"  TEST_DB_PORT={Environment.GetEnvironmentVariable("TEST_DB_PORT") ?? "(not set)"}\n" +
                $"  TEST_DB_USER={Environment.GetEnvironmentVariable("TEST_DB_USER") ?? "(not set)"}\n\n" +
                $"For Windows: Ensure SQL Server LocalDB is installed and (localdb)\\MSSQLLocalDB exists.\n" +
                $"For Linux/Docker: Run 'docker run -e ACCEPT_EULA=Y -e SA_PASSWORD=YourPassword123! " +
                $"-p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest' first.",
                ex);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Defines the xUnit test collection that shares a single <see cref="DatabaseFixture"/>.
/// </summary>
[CollectionDefinition(DatabaseFixture.CollectionName)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;

