using Microsoft.EntityFrameworkCore;
using SharpFunctional.MsSql.Common;
using SharpFunctional.MsSql.Ef;
using SharpFunctional.MsSql.Functional;
using Xunit;

namespace SharpFunctional.MsSql.Tests;

public sealed class TestEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
        });
    }
}

[Collection(DatabaseFixture.CollectionName)]
public class EfFunctionalDbTests(DatabaseFixture fixture) : IDisposable
{
    private readonly TestDbContext _dbContext = fixture.CreateDbContext();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        // Clean up test data so the next test class starts with a clean slate
        _dbContext.TestEntities.RemoveRange(_dbContext.TestEntities);
        _dbContext.SaveChanges();
        _dbContext.Dispose();
    }

    // --- GetByIdAsync ---

    [Fact]
    public async Task GetByIdAsync_WithExistingEntity_ShouldReturnSome()
    {
        // Arrange
        var entity = new TestEntity { Name = "Widget", Price = 9.99m };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.GetByIdAsync<TestEntity, int>(entity.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSome);
        result.IfSome(e => Assert.Equal("Widget", e.Name));
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ShouldReturnNone()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.GetByIdAsync<TestEntity, int>(999, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task GetByIdAsync_WithNullContext_ShouldReturnNone()
    {
        // Arrange
        var ef = new EfFunctionalDb(null);

        // Act
        var result = await ef.GetByIdAsync<TestEntity, int>(1, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsNone);
    }

    // --- FindOneAsync ---

    [Fact]
    public async Task FindOneAsync_WithMatchingPredicate_ShouldReturnSome()
    {
        // Arrange
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Alpha", Price = 1.0m }, TestContext.Current.CancellationToken);
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Beta", Price = 2.0m }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.FindOneAsync<TestEntity>(e => e.Name == "Beta", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSome);
        result.IfSome(e => Assert.Equal(2.0m, e.Price));
    }

    [Fact]
    public async Task FindOneAsync_WithNoMatch_ShouldReturnNone()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.FindOneAsync<TestEntity>(e => e.Name == "NonExistent", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsNone);
    }

    // --- QueryAsync ---

    [Fact]
    public async Task QueryAsync_WithMatchingEntities_ShouldReturnSeq()
    {
        // Arrange
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Item1", Price = 10.0m }, TestContext.Current.CancellationToken);
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Item2", Price = 20.0m }, TestContext.Current.CancellationToken);
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Other", Price = 30.0m }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.QueryAsync<TestEntity>(e => e.Name.StartsWith("Item"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task QueryAsync_WithNoMatch_ShouldReturnEmptySeq()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.QueryAsync<TestEntity>(e => e.Price > 1_000_000m, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsEmpty);
    }

    // --- AddAsync ---

    [Fact]
    public async Task AddAsync_WithValidEntity_ShouldReturnSuccess()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);
        var entity = new TestEntity { Name = "NewItem", Price = 5.0m };

        // Act
        var result = await ef.AddAsync(entity, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
    }

    [Fact]
    public async Task AddAsync_WithNullEntity_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.AddAsync<TestEntity>(null!, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task AddAsync_WithNullContext_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(null);

        // Act
        var result = await ef.AddAsync(new TestEntity { Name = "X", Price = 1.0m }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    // --- SaveAsync ---

    [Fact]
    public async Task SaveAsync_WithNewlyAddedTrackedEntity_ShouldPersistInsert()
    {
        // Arrange
        var entity = new TestEntity { Name = "Inserted", Price = 12.5m };
        var ef = new EfFunctionalDb(_dbContext).WithTracking();
        var addResult = await ef.AddAsync(entity, TestContext.Current.CancellationToken);

        // Act
        var result = await ef.SaveAsync(entity, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(addResult.IsSucc);
        Assert.True(result.IsSucc);
        Assert.True(entity.Id > 0);
        var inserted = await _dbContext.TestEntities.FindAsync([entity.Id], TestContext.Current.CancellationToken);
        Assert.Equal("Inserted", inserted!.Name);
    }

    [Fact]
    public async Task SaveAsync_WithTrackedEntity_ShouldPersistChanges()
    {
        // Arrange
        var entity = new TestEntity { Name = "Original", Price = 1.0m };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        entity.Name = "Updated";
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.SaveAsync(entity, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        var updated = await _dbContext.TestEntities.FindAsync([entity.Id], TestContext.Current.CancellationToken);
        Assert.Equal("Updated", updated!.Name);
    }

    [Fact]
    public async Task SaveAsync_WithNullEntity_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.SaveAsync<TestEntity>(null!, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task SaveAsync_WithCanceledToken_ShouldReturnFail()
    {
        // Arrange
        var entity = new TestEntity { Name = "Cancelable", Price = 1.0m };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ef = new EfFunctionalDb(_dbContext).WithTracking();
        entity.Name = "CanceledUpdate";

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await ef.SaveAsync(entity, cts.Token);

        // Assert
        Assert.True(result.IsFail);
    }

    // --- DeleteByIdAsync ---

    [Fact]
    public async Task DeleteByIdAsync_WithExistingEntity_ShouldRemoveEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "ToDelete", Price = 1.0m };
        await _dbContext.TestEntities.AddAsync(entity, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.DeleteByIdAsync<TestEntity, int>(entity.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        var found = await _dbContext.TestEntities.FindAsync([entity.Id], TestContext.Current.CancellationToken);
        Assert.Null(found);
    }

    [Fact]
    public async Task DeleteByIdAsync_WithNonExistingId_ShouldReturnSuccess()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.DeleteByIdAsync<TestEntity, int>(999, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
    }

    // --- CountAsync ---

    [Fact]
    public async Task CountAsync_WithMatchingEntities_ShouldReturnCount()
    {
        // Arrange
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "A", Price = 10.0m }, TestContext.Current.CancellationToken);
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "B", Price = 20.0m }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.CountAsync<TestEntity>(e => e.Price >= 10.0m, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(count => Assert.Equal(2, count));
    }

    // --- AnyAsync ---

    [Fact]
    public async Task AnyAsync_WithMatchingEntity_ShouldReturnTrue()
    {
        // Arrange
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Exists", Price = 1.0m }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.AnyAsync<TestEntity>(e => e.Name == "Exists", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(any => Assert.True(any));
    }

    [Fact]
    public async Task AnyAsync_WithNoMatch_ShouldReturnFalse()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.AnyAsync<TestEntity>(e => e.Name == "Ghost", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(any => Assert.False(any));
    }

    [Fact]
    public async Task WithTracking_ShouldReturnTrackedEntities()
    {
        // Arrange
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Tracked", Price = 1.0m }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();
        var ef = new EfFunctionalDb(_dbContext).WithTracking();

        // Act
        var result = await ef.FindOneAsync<TestEntity>(e => e.Name == "Tracked", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSome);
        Assert.Single(_dbContext.ChangeTracker.Entries());
    }

    // --- FindPaginatedAsync ---

    [Fact]
    public async Task FindPaginatedAsync_WithMultiplePages_ShouldReturnCorrectPage()
    {
        // Arrange
        for (var i = 1; i <= 10; i++)
        {
            await _dbContext.TestEntities.AddAsync(new TestEntity { Name = $"Page{i}", Price = i }, TestContext.Current.CancellationToken);
        }
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.FindPaginatedAsync<TestEntity>(e => e.Name.StartsWith("Page"), pageNumber: 2, pageSize: 3, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(page =>
        {
            Assert.Equal(10, page.TotalCount);
            Assert.Equal(2, page.PageNumber);
            Assert.Equal(3, page.PageSize);
            Assert.Equal(3, page.ItemsOnPage);
            Assert.True(page.HasNextPage);
            Assert.True(page.HasPreviousPage);
        });
    }

    [Fact]
    public async Task FindPaginatedAsync_BeyondLastPage_ShouldReturnEmptyItems()
    {
        // Arrange
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Solo", Price = 1.0m }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.FindPaginatedAsync<TestEntity>(e => e.Name == "Solo", pageNumber: 5, pageSize: 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(page =>
        {
            Assert.Equal(1, page.TotalCount);
            Assert.Empty(page.Items);
            Assert.False(page.HasNextPage);
        });
    }

    [Fact]
    public async Task FindPaginatedAsync_WithEmptyDataset_ShouldReturnZeroTotal()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.FindPaginatedAsync<TestEntity>(e => e.Name == "NonExistent", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(page =>
        {
            Assert.Equal(0, page.TotalCount);
            Assert.Empty(page.Items);
            Assert.Equal(0, page.TotalPages);
        });
    }

    [Fact]
    public async Task FindPaginatedAsync_WithNullPredicate_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.FindPaginatedAsync<TestEntity>(null!, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task FindPaginatedAsync_WithNullContext_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(null);

        // Act
        var result = await ef.FindPaginatedAsync<TestEntity>(e => e.Id > 0, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    // --- FindAsync (specification) ---

    [Fact]
    public async Task FindAsync_WithSpecification_ShouldFilterEntities()
    {
        // Arrange
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "SpecA", Price = 10.0m }, TestContext.Current.CancellationToken);
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "SpecB", Price = 20.0m }, TestContext.Current.CancellationToken);
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Other", Price = 30.0m }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);
        var spec = new QuerySpecification<TestEntity>(e => e.Name.StartsWith("Spec"));

        // Act
        var result = await ef.FindAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSome);
        result.IfSome(items => Assert.Equal(2, items.Count));
    }

    [Fact]
    public async Task FindAsync_WithSpecificationSkipAndTake_ShouldApplyPaging()
    {
        // Arrange
        for (var i = 1; i <= 5; i++)
        {
            await _dbContext.TestEntities.AddAsync(new TestEntity { Name = $"Paged{i}", Price = i }, TestContext.Current.CancellationToken);
        }
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);
        var spec = new QuerySpecification<TestEntity>(e => e.Name.StartsWith("Paged"))
            .SetOrderBy(e => e.Price)
            .SetSkip(2)
            .SetTake(2);

        // Act
        var result = await ef.FindAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSome);
        result.IfSome(items => Assert.Equal(2, items.Count));
    }

    [Fact]
    public async Task FindAsync_WithSpecificationAndNullContext_ShouldReturnNone()
    {
        // Arrange
        var ef = new EfFunctionalDb(null);
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0);

        // Act
        var result = await ef.FindAsync(spec, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsNone);
    }

    // --- InsertBatchAsync ---

    [Fact]
    public async Task InsertBatchAsync_WithMultipleEntities_ShouldInsertAll()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);
        var entities = Enumerable.Range(1, 10).Select(i => new TestEntity { Name = $"Batch{i}", Price = i }).ToList();

        // Act
        var result = await ef.InsertBatchAsync(entities, batchSize: 3, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(count => Assert.Equal(10, count));
        Assert.Equal(10, await _dbContext.TestEntities.CountAsync(e => e.Name.StartsWith("Batch"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InsertBatchAsync_WithEmptyCollection_ShouldReturnZero()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.InsertBatchAsync(Enumerable.Empty<TestEntity>(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(count => Assert.Equal(0, count));
    }

    [Fact]
    public async Task InsertBatchAsync_WithNullEntities_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.InsertBatchAsync<TestEntity>(null!, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task InsertBatchAsync_WithNullContext_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(null);

        // Act
        var result = await ef.InsertBatchAsync([new TestEntity { Name = "X", Price = 1 }], cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    // --- StreamAsync ---

    [Fact]
    public async Task StreamAsync_WithMatchingEntities_ShouldYieldAll()
    {
        // Arrange
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Stream1", Price = 1.0m }, TestContext.Current.CancellationToken);
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Stream2", Price = 2.0m }, TestContext.Current.CancellationToken);
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "Other", Price = 3.0m }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var items = new List<TestEntity>();
        await foreach (var entity in ef.StreamAsync<TestEntity>(e => e.Name.StartsWith("Stream"), TestContext.Current.CancellationToken))
        {
            items.Add(entity);
        }

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task StreamAsync_WithNoMatch_ShouldYieldNothing()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var items = new List<TestEntity>();
        await foreach (var entity in ef.StreamAsync<TestEntity>(e => e.Name == "Ghost", TestContext.Current.CancellationToken))
        {
            items.Add(entity);
        }

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public async Task StreamAsync_WithNullContext_ShouldYieldNothing()
    {
        // Arrange
        var ef = new EfFunctionalDb(null);

        // Act
        var items = new List<TestEntity>();
        await foreach (var entity in ef.StreamAsync<TestEntity>(e => e.Id > 0, TestContext.Current.CancellationToken))
        {
            items.Add(entity);
        }

        // Assert
        Assert.Empty(items);
    }

    // --- UpdateBatchAsync ---

    [Fact]
    public async Task UpdateBatchAsync_WithTrackedEntities_ShouldUpdateAll()
    {
        // Arrange
        var entities = Enumerable.Range(1, 5).Select(i => new TestEntity { Name = $"UpdBatch{i}", Price = i }).ToList();
        await _dbContext.TestEntities.AddRangeAsync(entities, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        foreach (var e in entities) e.Price += 100;
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.UpdateBatchAsync(entities, batchSize: 2, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(count => Assert.Equal(5, count));
        var prices = await _dbContext.TestEntities
            .Where(e => e.Name.StartsWith("UpdBatch"))
            .Select(e => e.Price)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(prices, p => Assert.True(p > 100));
    }

    [Fact]
    public async Task UpdateBatchAsync_WithEmptyCollection_ShouldReturnZero()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.UpdateBatchAsync(Enumerable.Empty<TestEntity>(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(count => Assert.Equal(0, count));
    }

    [Fact]
    public async Task UpdateBatchAsync_WithNullEntities_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.UpdateBatchAsync<TestEntity>(null!, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task UpdateBatchAsync_WithNullContext_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(null);

        // Act
        var result = await ef.UpdateBatchAsync([new TestEntity { Name = "X", Price = 1 }], cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    // --- DeleteBatchAsync ---

    [Fact]
    public async Task DeleteBatchAsync_WithMatchingEntities_ShouldDeleteAll()
    {
        // Arrange
        for (var i = 1; i <= 6; i++)
        {
            await _dbContext.TestEntities.AddAsync(new TestEntity { Name = $"DelBatch{i}", Price = i }, TestContext.Current.CancellationToken);
        }
        await _dbContext.TestEntities.AddAsync(new TestEntity { Name = "KeepMe", Price = 99 }, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.DeleteBatchAsync<TestEntity>(e => e.Name.StartsWith("DelBatch"), batchSize: 2, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(count => Assert.Equal(6, count));
        Assert.False(await _dbContext.TestEntities.AnyAsync(e => e.Name.StartsWith("DelBatch"), TestContext.Current.CancellationToken));
        Assert.True(await _dbContext.TestEntities.AnyAsync(e => e.Name == "KeepMe", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteBatchAsync_WithNoMatch_ShouldReturnZero()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.DeleteBatchAsync<TestEntity>(e => e.Name == "NonExistent", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSucc);
        result.IfSucc(count => Assert.Equal(0, count));
    }

    [Fact]
    public async Task DeleteBatchAsync_WithNullPredicate_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(_dbContext);

        // Act
        var result = await ef.DeleteBatchAsync<TestEntity>(null!, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }

    [Fact]
    public async Task DeleteBatchAsync_WithNullContext_ShouldReturnFail()
    {
        // Arrange
        var ef = new EfFunctionalDb(null);

        // Act
        var result = await ef.DeleteBatchAsync<TestEntity>(e => e.Id > 0, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsFail);
    }
}
