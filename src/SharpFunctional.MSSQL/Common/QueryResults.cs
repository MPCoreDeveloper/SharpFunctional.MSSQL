namespace SharpFunctional.MsSql.Common;

/// <summary>
/// Represents a paginated query result with metadata for navigation.
/// Supports functional transformations via <see cref="Map{TResult}"/>.
/// </summary>
/// <typeparam name="T">The result item type.</typeparam>
/// <param name="Items">The items on the current page.</param>
/// <param name="TotalCount">The total number of items matching the query across all pages.</param>
/// <param name="PageNumber">The current page number (1-based).</param>
/// <param name="PageSize">The maximum number of items per page.</param>
/// <example>
/// <code>
/// var page = await db.Ef().FindPaginatedAsync&lt;User&gt;(u =&gt; u.IsActive, pageNumber: 2, pageSize: 50);
/// page.Match(
///     Succ: results =&gt; Console.WriteLine($"Page {results.PageNumber}/{results.TotalPages}"),
///     Fail: error  =&gt; Console.WriteLine(error));
/// </code>
/// </example>
public sealed record QueryResults<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (TotalCount + PageSize - 1) / PageSize : 0;

    /// <summary>
    /// Indicates whether a next page exists beyond the current one.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Indicates whether a previous page exists before the current one.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Gets the number of items on this page.
    /// </summary>
    public int ItemsOnPage => Items.Count;

    /// <summary>
    /// Projects each item to a new type while preserving pagination metadata.
    /// </summary>
    /// <typeparam name="TResult">The projected item type.</typeparam>
    /// <param name="selector">Projection function applied to each item.</param>
    /// <returns>A new <see cref="QueryResults{TResult}"/> with projected items and the same pagination metadata.</returns>
    public QueryResults<TResult> Map<TResult>(Func<T, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new QueryResults<TResult>(
            Items.Select(selector).ToList().AsReadOnly(),
            TotalCount,
            PageNumber,
            PageSize);
    }

    /// <summary>
    /// Returns an empty result set for the given page parameters.
    /// </summary>
    /// <param name="pageNumber">The requested page number.</param>
    /// <param name="pageSize">The requested page size.</param>
    public static QueryResults<T> Empty(int pageNumber, int pageSize) =>
        new(Array.Empty<T>(), TotalCount: 0, pageNumber, pageSize);
}
