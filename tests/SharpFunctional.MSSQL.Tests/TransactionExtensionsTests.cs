using Microsoft.EntityFrameworkCore;
using SharpFunctional.MsSql.Functional;
using SharpFunctional.MsSql.Transactions;
using Xunit;
using static SharpFunctional.MsSql.Functional.Prelude;

namespace SharpFunctional.MsSql.Tests;

[Collection(DatabaseFixture.CollectionName)]
public class TransactionExtensionsTests(DatabaseFixture fixture) : IDisposable
{
    private readonly TestDbContext _dbContext = fixture.CreateDbContext();

    public void Dispose()
    {
        _dbContext.TestEntities.RemoveRange(_dbContext.TestEntities);
        _dbContext.SaveChanges();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task InTransactionMapAsync_WithSuccessAction_ShouldMapResult()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);

        // Act
        var result = await db.InTransactionMapAsync(
            async _ =>
            {
                await Task.CompletedTask;
                return Fin<int>.Succ(42);
            },
            value => $"mapped:{value}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(v => Assert.Equal("mapped:42", v));
    }

    [Fact]
    public async Task InTransactionMapAsync_WithFailAction_ShouldPropagateError()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);

        // Act
        var result = await db.InTransactionMapAsync(
            async _ =>
            {
                await Task.CompletedTask;
                return FinFail<int>(Error.New("oops"));
            },
            value => $"mapped:{value}",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task InTransactionMapAsync_WithNullAction_ShouldReturnFail()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);

        // Act
        var result = await db.InTransactionMapAsync<int, string>(null!, value => $"mapped:{value}", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task InTransactionMapAsync_WithCanceledToken_ShouldReturnFail()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await db.InTransactionMapAsync(
            async _ =>
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
                return Fin<int>.Succ(42);
            },
            value => $"mapped:{value}",
            cts.Token);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task InTransactionMapAsync_WithNullMap_ShouldReturnFail()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);

        // Act
        var result = await db.InTransactionMapAsync(
            async _ =>
            {
                await Task.CompletedTask;
                return Fin<int>.Succ(42);
            },
            (Func<int, string>)null!,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }
}
