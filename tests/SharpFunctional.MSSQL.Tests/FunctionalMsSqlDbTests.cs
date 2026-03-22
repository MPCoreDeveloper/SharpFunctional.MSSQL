using LanguageExt;
using LanguageExt.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SharpFunctional.MsSql.Dapper;
using SharpFunctional.MsSql.Ef;
using Xunit;
using static LanguageExt.Prelude;

namespace SharpFunctional.MsSql.Tests;

[Collection(DatabaseFixture.CollectionName)]
public class FunctionalMsSqlDbTests(DatabaseFixture fixture) : IDisposable
{
    private readonly SqlConnection _connection = DatabaseFixture.CreateOpenConnection();
    private readonly TestDbContext _dbContext = fixture.CreateDbContext();

    public void Dispose()
    {
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
}
