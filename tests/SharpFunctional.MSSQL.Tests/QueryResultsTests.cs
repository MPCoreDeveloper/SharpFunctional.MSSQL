using SharpFunctional.MsSql.Common;
using Xunit;

namespace SharpFunctional.MsSql.Tests;

public class QueryResultsTests
{
    // --- TotalPages ---

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(100, 25, 4)]
    [InlineData(101, 25, 5)]
    public void TotalPages_WithVariousCounts_ShouldCalculateCorrectly(int totalCount, int pageSize, int expectedPages)
    {
        // Arrange
        var result = new QueryResults<string>([], totalCount, PageNumber: 1, pageSize);

        // Assert
        Assert.Equal(expectedPages, result.TotalPages);
    }

    [Fact]
    public void TotalPages_WithZeroPageSize_ShouldReturnZero()
    {
        // Arrange
        var result = new QueryResults<string>([], TotalCount: 10, PageNumber: 1, PageSize: 0);

        // Assert
        Assert.Equal(0, result.TotalPages);
    }

    // --- HasNextPage ---

    [Fact]
    public void HasNextPage_WhenOnFirstPageOfMany_ShouldReturnTrue()
    {
        // Arrange
        var result = new QueryResults<int>([1, 2], TotalCount: 10, PageNumber: 1, PageSize: 2);

        // Assert
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void HasNextPage_WhenOnLastPage_ShouldReturnFalse()
    {
        // Arrange
        var result = new QueryResults<int>([9, 10], TotalCount: 10, PageNumber: 5, PageSize: 2);

        // Assert
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void HasNextPage_WhenSinglePage_ShouldReturnFalse()
    {
        // Arrange
        var result = new QueryResults<int>([1], TotalCount: 1, PageNumber: 1, PageSize: 10);

        // Assert
        Assert.False(result.HasNextPage);
    }

    // --- HasPreviousPage ---

    [Fact]
    public void HasPreviousPage_WhenOnFirstPage_ShouldReturnFalse()
    {
        // Arrange
        var result = new QueryResults<int>([1, 2], TotalCount: 10, PageNumber: 1, PageSize: 2);

        // Assert
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void HasPreviousPage_WhenOnSecondPage_ShouldReturnTrue()
    {
        // Arrange
        var result = new QueryResults<int>([3, 4], TotalCount: 10, PageNumber: 2, PageSize: 2);

        // Assert
        Assert.True(result.HasPreviousPage);
    }

    // --- ItemsOnPage ---

    [Fact]
    public void ItemsOnPage_WithPartialLastPage_ShouldReturnActualCount()
    {
        // Arrange
        var result = new QueryResults<int>([9], TotalCount: 9, PageNumber: 5, PageSize: 2);

        // Assert
        Assert.Equal(1, result.ItemsOnPage);
    }

    [Fact]
    public void ItemsOnPage_WithFullPage_ShouldReturnPageSize()
    {
        // Arrange
        var result = new QueryResults<int>([1, 2, 3], TotalCount: 9, PageNumber: 1, PageSize: 3);

        // Assert
        Assert.Equal(3, result.ItemsOnPage);
    }

    // --- Map ---

    [Fact]
    public void Map_WithValidSelector_ShouldProjectItemsAndPreserveMetadata()
    {
        // Arrange
        var result = new QueryResults<int>([1, 2, 3], TotalCount: 30, PageNumber: 2, PageSize: 3);

        // Act
        var mapped = result.Map(x => x.ToString());

        // Assert
        Assert.Equal(["1", "2", "3"], mapped.Items);
        Assert.Equal(30, mapped.TotalCount);
        Assert.Equal(2, mapped.PageNumber);
        Assert.Equal(3, mapped.PageSize);
    }

    [Fact]
    public void Map_WithNullSelector_ShouldThrowArgumentNullException()
    {
        // Arrange
        var result = new QueryResults<int>([1], TotalCount: 1, PageNumber: 1, PageSize: 10);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => result.Map<string>(null!));
    }

    // --- Empty ---

    [Fact]
    public void Empty_ShouldReturnZeroItemsWithMetadata()
    {
        // Act
        var result = QueryResults<int>.Empty(pageNumber: 3, pageSize: 25);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasNextPage);
    }
}
