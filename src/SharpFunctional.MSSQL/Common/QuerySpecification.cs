using System.Linq.Expressions;

namespace SharpFunctional.MsSql.Common;

/// <summary>
/// Defines a reusable, composable query specification that encapsulates filter,
/// include, ordering, and paging logic for EF Core queries.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IQuerySpecification<T> where T : class
{
    /// <summary>Filter predicate.</summary>
    Expression<Func<T, bool>>? Predicate { get; }

    /// <summary>Navigation properties to eagerly load.</summary>
    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }

    /// <summary>Order-by key selector (applied as the primary sort).</summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>When <see langword="true"/>, the primary sort is descending.</summary>
    bool IsDescending { get; }

    /// <summary>Number of items to skip.</summary>
    int? Skip { get; }

    /// <summary>Number of items to take.</summary>
    int? Take { get; }
}

/// <summary>
/// Fluent builder for <see cref="IQuerySpecification{T}"/>.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <example>
/// <code>
/// var spec = new QuerySpecification&lt;Order&gt;(o =&gt; o.Total &gt; 1000)
///     .AddInclude(o =&gt; o.Customer)
///     .SetOrderByDescending(o =&gt; o.OrderDate)
///     .SetSkip(50)
///     .SetTake(25);
///
/// var orders = await db.Ef().FindAsync(spec);
/// </code>
/// </example>
public sealed class QuerySpecification<T> : IQuerySpecification<T> where T : class
{
    private readonly List<Expression<Func<T, object>>> _includes = [];

    /// <summary>
    /// Creates a new specification with the given filter predicate.
    /// </summary>
    /// <param name="predicate">The filter expression.</param>
    public QuerySpecification(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        Predicate = predicate;
    }

    /// <inheritdoc />
    public Expression<Func<T, bool>>? Predicate { get; }

    /// <inheritdoc />
    public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes;

    /// <inheritdoc />
    public Expression<Func<T, object>>? OrderBy { get; private set; }

    /// <inheritdoc />
    public bool IsDescending { get; private set; }

    /// <inheritdoc />
    public int? Skip { get; private set; }

    /// <inheritdoc />
    public int? Take { get; private set; }

    /// <summary>
    /// Adds a navigation property to eagerly load.
    /// </summary>
    /// <param name="includeExpression">The navigation property expression.</param>
    public QuerySpecification<T> AddInclude(Expression<Func<T, object>> includeExpression)
    {
        ArgumentNullException.ThrowIfNull(includeExpression);
        _includes.Add(includeExpression);
        return this;
    }

    /// <summary>
    /// Sets the primary sort key in ascending order.
    /// </summary>
    /// <param name="keySelector">The sort key expression.</param>
    public QuerySpecification<T> SetOrderBy(Expression<Func<T, object>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        OrderBy = keySelector;
        IsDescending = false;
        return this;
    }

    /// <summary>
    /// Sets the primary sort key in descending order.
    /// </summary>
    /// <param name="keySelector">The sort key expression.</param>
    public QuerySpecification<T> SetOrderByDescending(Expression<Func<T, object>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        OrderBy = keySelector;
        IsDescending = true;
        return this;
    }

    /// <summary>
    /// Sets the number of items to skip.
    /// </summary>
    /// <param name="count">Items to skip.</param>
    public QuerySpecification<T> SetSkip(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Skip = count;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of items to return.
    /// </summary>
    /// <param name="count">Maximum items.</param>
    public QuerySpecification<T> SetTake(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        Take = count;
        return this;
    }
}
