using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SharpFunctional.MsSql.Common;
using SharpFunctional.MsSql.Ef;
using Xunit;

namespace SharpFunctional.MsSql.Tests;

[Collection(DatabaseFixture.CollectionName)]
public class OpenTelemetryInstrumentationTests(DatabaseFixture fixture) : IDisposable
{
    private readonly TestDbContext _dbContext = fixture.CreateDbContext();
    private readonly SqlConnection _connection = DatabaseFixture.CreateOpenConnection();

    public void Dispose()
    {
        _dbContext.TestEntities.RemoveRange(_dbContext.TestEntities);
        _dbContext.SaveChanges();
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task InTransactionAsync_ShouldEmitTransactionActivity()
    {
        // Arrange
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);

        // Act
        var result = await db.InTransactionAsync(async _ =>
        {
            await Task.CompletedTask;
            return LanguageExt.Fin<int>.Succ(1);
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        var activity = activities.LastOrDefault(a => a.OperationName == "sharpfunctional.mssql.transaction");
        Assert.NotNull(activity);
        Assert.Equal("ef", activity!.GetTagItem(SharpFunctionalMsSqlDiagnostics.BackendTag));
        Assert.Equal(true, activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.SuccessTag));
    }

    [Fact]
    public async Task QuerySingleAsync_ShouldEmitDapperActivity()
    {
        // Arrange
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        var db = new FunctionalMsSqlDb(connection: _connection);

        // Act
        var result = await db.Dapper().QuerySingleAsync<int>("SELECT 1", new { }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSome);
        var activity = activities.LastOrDefault(a => a.OperationName == "sharpfunctional.mssql.dapper");
        Assert.NotNull(activity);
        Assert.Equal("dapper", activity!.GetTagItem(SharpFunctionalMsSqlDiagnostics.BackendTag));
        Assert.Equal("dapper.query.single", activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.OperationTag));
        Assert.Equal(true, activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.SuccessTag));
    }

    [Fact]
    public async Task FindPaginatedAsync_ShouldEmitEfPaginatedActivity()
    {
        // Arrange
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);

        // Act
        var result = await db.Ef().FindPaginatedAsync<TestEntity>(
            e => e.Id > 0,
            pageNumber: 1,
            pageSize: 10,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        var activity = activities.LastOrDefault(a => a.OperationName == "sharpfunctional.mssql.ef");
        Assert.NotNull(activity);
        Assert.Equal("ef", activity!.GetTagItem(SharpFunctionalMsSqlDiagnostics.BackendTag));
        Assert.Equal("ef.find.paginated", activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.OperationTag));
        Assert.Equal("TestEntity", activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.EntityTypeTag));
        Assert.Equal(1, activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.PageNumberTag));
        Assert.Equal(10, activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.PageSizeTag));
        Assert.Equal(true, activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.SuccessTag));
    }

    [Fact]
    public async Task InsertBatchAsync_ShouldEmitEfBatchInsertActivity()
    {
        // Arrange
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);
        var entities = new[] { new TestEntity { Name = "OTel1" }, new TestEntity { Name = "OTel2" } };

        // Act
        var result = await db.Ef().InsertBatchAsync(entities, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        var activity = activities.LastOrDefault(a =>
            a.OperationName == "sharpfunctional.mssql.ef" &&
            a.GetTagItem(SharpFunctionalMsSqlDiagnostics.OperationTag) as string == "ef.batch.insert");
        Assert.NotNull(activity);
        Assert.Equal("ef", activity!.GetTagItem(SharpFunctionalMsSqlDiagnostics.BackendTag));
        Assert.Equal("TestEntity", activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.EntityTypeTag));
        Assert.Equal(true, activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.SuccessTag));
    }

    [Fact]
    public async Task UpdateBatchAsync_ShouldEmitEfBatchUpdateActivity()
    {
        // Arrange
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);
        var entity = new TestEntity { Name = "OTelUpdate" };
        _dbContext.TestEntities.Add(entity);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        entity.Name = "OTelUpdated";

        // Act
        var result = await db.Ef().WithTracking().UpdateBatchAsync(
            new[] { entity },
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        var activity = activities.LastOrDefault(a =>
            a.OperationName == "sharpfunctional.mssql.ef" &&
            a.GetTagItem(SharpFunctionalMsSqlDiagnostics.OperationTag) as string == "ef.batch.update");
        Assert.NotNull(activity);
        Assert.Equal("ef", activity!.GetTagItem(SharpFunctionalMsSqlDiagnostics.BackendTag));
        Assert.Equal("TestEntity", activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.EntityTypeTag));
        Assert.Equal(true, activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.SuccessTag));
    }

    [Fact]
    public async Task DeleteBatchAsync_ShouldEmitEfBatchDeleteActivity()
    {
        // Arrange
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);
        _dbContext.TestEntities.Add(new TestEntity { Name = "OTelDelete" });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await db.Ef().DeleteBatchAsync<TestEntity>(
            e => e.Name == "OTelDelete",
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        var activity = activities.LastOrDefault(a =>
            a.OperationName == "sharpfunctional.mssql.ef" &&
            a.GetTagItem(SharpFunctionalMsSqlDiagnostics.OperationTag) as string == "ef.batch.delete");
        Assert.NotNull(activity);
        Assert.Equal("ef", activity!.GetTagItem(SharpFunctionalMsSqlDiagnostics.BackendTag));
        Assert.Equal("TestEntity", activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.EntityTypeTag));
        Assert.Equal(true, activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.SuccessTag));
    }

    [Fact]
    public async Task FindAsync_WithSpecification_ShouldEmitEfFindSpecActivity()
    {
        // Arrange
        var activities = new List<Activity>();
        using var listener = CreateListener(activities);
        var db = new FunctionalMsSqlDb(dbContext: _dbContext);
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0);

        // Act
        var result = await db.Ef().FindAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSome);
        var activity = activities.LastOrDefault(a =>
            a.OperationName == "sharpfunctional.mssql.ef" &&
            a.GetTagItem(SharpFunctionalMsSqlDiagnostics.OperationTag) as string == "ef.find.spec");
        Assert.NotNull(activity);
        Assert.Equal("ef", activity!.GetTagItem(SharpFunctionalMsSqlDiagnostics.BackendTag));
        Assert.Equal("TestEntity", activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.EntityTypeTag));
        Assert.Equal(true, activity.GetTagItem(SharpFunctionalMsSqlDiagnostics.SuccessTag));
    }

    private static ActivityListener CreateListener(List<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "SharpFunctional.MsSql",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = _ => { },
            ActivityStopped = activity => activities.Add(activity)
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
