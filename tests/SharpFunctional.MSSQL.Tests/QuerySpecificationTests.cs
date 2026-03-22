using SharpFunctional.MsSql.Common;
using Xunit;

namespace SharpFunctional.MsSql.Tests;

public class QuerySpecificationTests
{
    // --- Constructor ---

    [Fact]
    public void Constructor_WithNullPredicate_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new QuerySpecification<TestEntity>(null!));
    }

    [Fact]
    public void Constructor_WithValidPredicate_ShouldSetPredicate()
    {
        // Act
        var spec = new QuerySpecification<TestEntity>(e => e.Price > 10);

        // Assert
        Assert.NotNull(spec.Predicate);
        Assert.Empty(spec.Includes);
        Assert.Null(spec.OrderBy);
        Assert.False(spec.IsDescending);
        Assert.Null(spec.Skip);
        Assert.Null(spec.Take);
    }

    // --- Fluent Chaining ---

    [Fact]
    public void FluentChain_WithAllMethods_ShouldSetAllProperties()
    {
        // Act
        var spec = new QuerySpecification<TestEntity>(e => e.Price > 0)
            .AddInclude(e => e.Name)
            .SetOrderByDescending(e => e.Price)
            .SetSkip(10)
            .SetTake(25);

        // Assert
        Assert.NotNull(spec.Predicate);
        Assert.Single(spec.Includes);
        Assert.NotNull(spec.OrderBy);
        Assert.True(spec.IsDescending);
        Assert.Equal(10, spec.Skip);
        Assert.Equal(25, spec.Take);
    }

    // --- AddInclude ---

    [Fact]
    public void AddInclude_WithMultipleIncludes_ShouldAccumulateAll()
    {
        // Act
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0)
            .AddInclude(e => e.Name)
            .AddInclude(e => e.Price);

        // Assert
        Assert.Equal(2, spec.Includes.Count);
    }

    [Fact]
    public void AddInclude_WithNullExpression_ShouldThrowArgumentNullException()
    {
        // Arrange
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => spec.AddInclude(null!));
    }

    // --- SetOrderBy / SetOrderByDescending ---

    [Fact]
    public void SetOrderBy_ShouldSetAscending()
    {
        // Act
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0)
            .SetOrderBy(e => e.Name);

        // Assert
        Assert.NotNull(spec.OrderBy);
        Assert.False(spec.IsDescending);
    }

    [Fact]
    public void SetOrderByDescending_ShouldOverridePreviousOrderBy()
    {
        // Act
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0)
            .SetOrderBy(e => e.Name)
            .SetOrderByDescending(e => e.Price);

        // Assert
        Assert.NotNull(spec.OrderBy);
        Assert.True(spec.IsDescending);
    }

    [Fact]
    public void SetOrderBy_WithNullSelector_ShouldThrowArgumentNullException()
    {
        // Arrange
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => spec.SetOrderBy(null!));
    }

    [Fact]
    public void SetOrderByDescending_WithNullSelector_ShouldThrowArgumentNullException()
    {
        // Arrange
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => spec.SetOrderByDescending(null!));
    }

    // --- SetSkip ---

    [Fact]
    public void SetSkip_WithZero_ShouldSucceed()
    {
        // Act
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0).SetSkip(0);

        // Assert
        Assert.Equal(0, spec.Skip);
    }

    [Fact]
    public void SetSkip_WithNegativeValue_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => spec.SetSkip(-1));
    }

    // --- SetTake ---

    [Fact]
    public void SetTake_WithPositiveValue_ShouldSucceed()
    {
        // Act
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0).SetTake(50);

        // Assert
        Assert.Equal(50, spec.Take);
    }

    [Fact]
    public void SetTake_WithZero_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => spec.SetTake(0));
    }

    [Fact]
    public void SetTake_WithNegativeValue_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var spec = new QuerySpecification<TestEntity>(e => e.Id > 0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => spec.SetTake(-5));
    }
}
