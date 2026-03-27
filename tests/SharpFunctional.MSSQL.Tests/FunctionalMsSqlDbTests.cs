using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharpFunctional.MsSql.Dapper;
using SharpFunctional.MsSql.Ef;
using SharpFunctional.MsSql.Functional;
using Xunit;
using static SharpFunctional.MsSql.Functional.Prelude;

namespace SharpFunctional.MsSql.Tests;

[Collection(DatabaseFixture.CollectionName)]
public class FunctionalMsSqlDbTests(DatabaseFixture fixture) : IDisposable
{
    private readonly SqlConnection _connection = DatabaseFixture.CreateOpenConnection();
    private readonly TestDbContext _dbContext = fixture.CreateDbContext();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _dbContext.TestEntities.RemoveRange(_dbContext.TestEntities);
        _dbContext.SaveChanges();
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Ef_ShouldReturnEfFunctionalDb()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);

        // Act
        var ef = db.Ef();

        // Assert
        Assert.IsType<EfFunctionalDb>(ef);
    }

    [Fact]
    public void Dapper_ShouldReturnDapperFunctionalDb()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(connection: _connection);

        // Act
        var dapper = db.Dapper();

        // Assert
        Assert.IsType<DapperFunctionalDb>(dapper);
    }

    [Fact]
    public async Task InTransactionAsync_WithNullAction_ShouldReturnFail()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);

        // Act
        var result = await db.InTransactionAsync<int>(null!, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task InTransactionAsync_WithSuccessAction_ShouldCommitAndReturnSuccess()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);

        // Act
        var result = await db.InTransactionAsync(async txDb =>
        {
            var ef = txDb.Ef();
            var addResult = await ef.AddAsync(new TestEntity { Name = "TxItem", Price = 10.0m }, TestContext.Current.CancellationToken);
            if (addResult.IsFail) return FinFail<string>(Error.New("Add failed"));
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            return Fin<string>.Succ("committed");
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(v => Assert.Equal("committed", v));
    }

    [Fact]
    public async Task InTransactionAsync_WithLoggerOnEfSuccess_ShouldWriteGeneratedDebugLogs()
    {
        // Arrange
        using var loggerFactory = TestLoggerFactory.Create();
        var db = new FunctionalMsSqlDb(
            dbContext: _dbContext,
            logger: loggerFactory.Factory.CreateLogger<FunctionalMsSqlDb>());

        // Act
        var result = await db.InTransactionAsync(async _ =>
        {
            await Task.CompletedTask;
            return Fin<string>.Succ("committed");
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Debug && entry.Message == "Starting EF transaction for result type String");
        Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Debug && entry.Message == "Committed EF transaction for result type String");
    }

    [Fact]
    public async Task InTransactionAsync_WithFailAction_ShouldRollback()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);
        var error = Error.New("deliberate failure");

        // Act
        var result = await db.InTransactionAsync<string>(async _ =>
        {
            await Task.CompletedTask;
            return FinFail<string>(error);
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task InTransactionAsync_WithLoggerOnDapperOpenFailure_ShouldWriteGeneratedErrorLog()
    {
        // Arrange
        using var loggerFactory = TestLoggerFactory.Create();
        var db = new FunctionalMsSqlDb(
            connection: new FailingOpenDbConnection(),
            logger: loggerFactory.Factory.CreateLogger<FunctionalMsSqlDb>());

        // Act
        var result = await db.InTransactionAsync(async _ =>
        {
            await Task.CompletedTask;
            return Fin<int>.Succ(1);
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
        Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Debug && entry.Message == "Starting Dapper transaction for result type Int32");
        Assert.Contains(loggerFactory.Entries, entry => entry.Level == LogLevel.Error && entry.Message == "SQL connection open failed after 1 attempt(s)" && entry.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task InTransactionAsync_WithCanceledToken_ShouldReturnFail()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await db.InTransactionAsync(
            async _ =>
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
                return Fin<int>.Succ(1);
            },
            cts.Token);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task InTransactionAsync_WithNoBackend_ShouldReturnFail()
    {
        // Arrange
        var db = new FunctionalMsSqlDb();

        // Act
        var result = await db.InTransactionAsync(async _ =>
        {
            await Task.CompletedTask;
            return Fin<int>.Succ(1);
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    private sealed record TestLogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class TestLoggerFactory : IDisposable
    {
        private readonly TestLoggerProvider _provider = new();

        private TestLoggerFactory()
        {
            Factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddProvider(_provider);
            });
        }

        public ILoggerFactory Factory { get; }

        public IReadOnlyList<TestLogEntry> Entries => _provider.Entries;

        public static TestLoggerFactory Create() => new();

        public void Dispose() => Factory.Dispose();
    }

    private sealed class TestLoggerProvider : ILoggerProvider
    {
        private readonly List<TestLogEntry> _entries = [];

        public IReadOnlyList<TestLogEntry> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new TestLogger(_entries);

        public void Dispose()
        {
        }
    }

    private sealed class TestLogger(List<TestLogEntry> entries) : ILogger
    {
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            entries.Add(new TestLogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class FailingOpenDbConnection : System.Data.Common.DbConnection
    {
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => "Test";

        public override string DataSource => "Test";

        public override string ServerVersion => "1.0";

        public override System.Data.ConnectionState State => System.Data.ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Close()
        {
        }

        public override void Open() => throw new InvalidOperationException("Open failed");

        public override Task OpenAsync(CancellationToken cancellationToken) => Task.FromException(new InvalidOperationException("Open failed"));

        protected override System.Data.Common.DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override System.Data.Common.DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}
