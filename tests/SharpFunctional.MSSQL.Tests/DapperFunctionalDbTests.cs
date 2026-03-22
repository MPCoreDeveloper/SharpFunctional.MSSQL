using LanguageExt;
using Xunit;

namespace SharpFunctional.MsSql.Tests;

public class DapperFunctionalDbTests
{
    [Fact]
    public async Task ExecuteStoredProcSingleAsync_WithNullConnection_ShouldReturnNone()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(connection: null);
        var dapper = db.Dapper();

        // Act
        var result = await dapper.ExecuteStoredProcSingleAsync<string>("dbo.Proc", new { Id = 1 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task ExecuteStoredProcSingleAsync_WithEmptyProcName_ShouldReturnNone()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(connection: null);
        var dapper = db.Dapper();

        // Act
        var result = await dapper.ExecuteStoredProcSingleAsync<string>("", new { Id = 1 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task ExecuteStoredProcAsync_WithNullConnection_ShouldReturnEmptySeq()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(connection: null);
        var dapper = db.Dapper();

        // Act
        var result = await dapper.ExecuteStoredProcAsync<string>("dbo.Proc", new { Status = 1 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task ExecuteStoredProcNonQueryAsync_WithNullConnection_ShouldReturnFail()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(connection: null);
        var dapper = db.Dapper();

        // Act
        var result = await dapper.ExecuteStoredProcNonQueryAsync("dbo.Proc", new { Id = 1 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task ExecuteStoredProcNonQueryAsync_WithEmptyProcName_ShouldReturnFail()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(connection: null);
        var dapper = db.Dapper();

        // Act
        var result = await dapper.ExecuteStoredProcNonQueryAsync("  ", new { Id = 1 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task QueryAsync_WithNullConnection_ShouldReturnEmptySeq()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(connection: null);
        var dapper = db.Dapper();

        // Act
        var result = await dapper.QueryAsync<string>("SELECT 1", new { }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task QuerySingleAsync_WithNullConnection_ShouldReturnNone()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(connection: null);
        var dapper = db.Dapper();

        // Act
        var result = await dapper.QuerySingleAsync<string>("SELECT 1", new { }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsNone);
    }

    // --- ExecuteStoredProcPaginatedAsync ---

    [Fact]
    public async Task ExecuteStoredProcPaginatedAsync_WithNullConnection_ShouldReturnFail()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(connection: null);
        var dapper = db.Dapper();

        // Act
        var result = await dapper.ExecuteStoredProcPaginatedAsync<string>("dbo.Proc", new { Status = 1 }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task ExecuteStoredProcPaginatedAsync_WithEmptyProcName_ShouldReturnFail()
    {
        // Arrange
        var db = new FunctionalMsSqlDb(connection: null);
        var dapper = db.Dapper();

        // Act
        var result = await dapper.ExecuteStoredProcPaginatedAsync<string>("", new { }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }
}
