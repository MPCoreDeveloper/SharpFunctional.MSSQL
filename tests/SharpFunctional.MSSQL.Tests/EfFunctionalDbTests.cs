using LanguageExt;
using Microsoft.EntityFrameworkCore;
using SharpFunctional.MsSql.Ef;
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
        _dbContext.TestEntities.Add(entity);
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
        _dbContext.TestEntities.Add(new TestEntity { Name = "Alpha", Price = 1.0m });
        _dbContext.TestEntities.Add(new TestEntity { Name = "Beta", Price = 2.0m });
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
        _dbContext.TestEntities.Add(new TestEntity { Name = "Item1", Price = 10.0m });
        _dbContext.TestEntities.Add(new TestEntity { Name = "Item2", Price = 20.0m });
        _dbContext.TestEntities.Add(new TestEntity { Name = "Other", Price = 30.0m });
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
    public async Task SaveAsync_WithTrackedEntity_ShouldPersistChanges()
    {
        // Arrange
        var entity = new TestEntity { Name = "Original", Price = 1.0m };
        _dbContext.TestEntities.Add(entity);
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
        _dbContext.TestEntities.Add(entity);
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
        _dbContext.TestEntities.Add(entity);
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
        _dbContext.TestEntities.Add(new TestEntity { Name = "A", Price = 10.0m });
        _dbContext.TestEntities.Add(new TestEntity { Name = "B", Price = 20.0m });
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
        _dbContext.TestEntities.Add(new TestEntity { Name = "Exists", Price = 1.0m });
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
        _dbContext.TestEntities.Add(new TestEntity { Name = "Tracked", Price = 1.0m });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();
        var ef = new EfFunctionalDb(_dbContext).WithTracking();

        // Act
        var result = await ef.FindOneAsync<TestEntity>(e => e.Name == "Tracked", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSome);
        Assert.Single(_dbContext.ChangeTracker.Entries());
    }
}
