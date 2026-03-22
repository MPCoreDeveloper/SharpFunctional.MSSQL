using LanguageExt;
using LanguageExt.Common;
using SharpFunctional.MsSql;
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

    /// <summary>
    /// Adds a product and saves in a transaction.
    /// Returns <see cref="Fin{T}"/> — success with the saved entity or failure with context.
    /// </summary>
    public Task<Fin<Product>> AddProductAsync(Product product, CancellationToken ct = default)
        => db.InTransactionAsync(async txDb =>
        {
            var add = await txDb.Ef().AddAsync(product, ct);
            if (add.IsFail)
                return FinFail<Product>(add.Error);

            var save = await txDb.Ef().WithTracking().SaveAsync(product, ct);
            return save.Map(_ => product);
        }, ct);
}

/// <summary>Dapper projection — lightweight product summary.</summary>
internal sealed record ProductSummary(string Name, string Category, decimal Price);
