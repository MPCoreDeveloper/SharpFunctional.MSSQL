using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SharpFunctional.MsSql.Common;
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
