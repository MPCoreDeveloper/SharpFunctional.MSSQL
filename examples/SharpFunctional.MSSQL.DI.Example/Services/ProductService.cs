using LanguageExt;
using LanguageExt.Common;
using SharpFunctional.MsSql;
using SharpFunctional.MsSql.Common;
using SharpFunctional.MsSql.Ef;
using SharpFunctional.MsSql.DiExample.Models;
using static LanguageExt.Prelude;

namespace SharpFunctional.MsSql.DiExample.Services;

/// <summary>
/// Example service that consumes <see cref="FunctionalMsSqlDb"/> via constructor injection.
/// All methods return functional types — no exceptions leak to the caller.
/// </summary>
public sealed class ProductService(FunctionalMsSqlDb db)
{
    /// <summary>Returns a single product by primary key, or None when not found.</summary>
    public Task<Option<Product>> GetByIdAsync(int id, CancellationToken ct = default)
        => db.Ef().GetByIdAsync<Product, int>(id, ct);

    /// <summary>Returns all products in a given category.</summary>
    public Task<Seq<Product>> GetByCategoryAsync(string category, CancellationToken ct = default)
        => db.Ef().QueryAsync<Product>(p => p.Category == category, ct);

    /// <summary>Returns the total number of products in stock.</summary>
    public Task<Fin<int>> CountInStockAsync(CancellationToken ct = default)
        => db.Ef().CountAsync<Product>(p => p.Stock > 0, ct);

    /// <summary>Returns a flat product list via raw Dapper SQL.</summary>
    public Task<Seq<ProductSummary>> GetSummariesAsync(CancellationToken ct = default)
        => db.Dapper().QueryAsync<ProductSummary>(
            "SELECT Name, Category, Price FROM Products ORDER BY Name",
            new { },
            ct);

    /// <summary>Returns a paginated set of products matching a category filter.</summary>
    public Task<Fin<QueryResults<Product>>> GetPaginatedAsync(
        string category,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken ct = default)
        => db.Ef().FindPaginatedAsync<Product>(p => p.Category == category, pageNumber, pageSize, ct);

    /// <summary>Returns products matching a reusable query specification.</summary>
    public Task<Option<IReadOnlyList<Product>>> GetBySpecificationAsync(
        IQuerySpecification<Product> specification,
        CancellationToken ct = default)
        => db.Ef().FindAsync(specification, ct);

    /// <summary>Inserts multiple products in configurable batches.</summary>
    public Task<Fin<int>> BatchInsertAsync(
        IEnumerable<Product> products,
        int batchSize = 100,
        CancellationToken ct = default)
        => db.Ef().InsertBatchAsync(products, batchSize, ct);

    /// <summary>Streams all products matching a predicate without full materialization.</summary>
    public IAsyncEnumerable<Product> StreamAllAsync(CancellationToken ct = default)
        => db.Ef().StreamAsync<Product>(p => p.Id > 0, ct);

    /// <summary>
    /// Adds a product and saves in a transaction.
    /// Returns <see cref="Fin{T}"/> — success with the saved entity or failure with context.
    /// </summary>
    public Task<Fin<Product>> AddProductAsync(Product product, CancellationToken ct = default)
        => db.InTransactionAsync(async txDb =>
        {
            var add = await txDb.Ef().AddAsync(product, ct);
            if (add.IsFail)
                return add.Match(
                    Succ: _ => FinFail<Product>("Unexpected success"),
                    Fail: FinFail<Product>);

            var save = await txDb.Ef().WithTracking().SaveAsync(product, ct);
            return save.Map(_ => product);
        }, ct);
}

/// <summary>Dapper projection — lightweight product summary.</summary>
public sealed record ProductSummary(string Name, string Category, decimal Price);
