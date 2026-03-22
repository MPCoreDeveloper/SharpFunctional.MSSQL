using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace SharpFunctional.MsSql.Transactions;

/// <summary>
/// Provides transaction-oriented functional helpers.
/// </summary>
public static class TransactionExtensions
{
    /// <summary>
    /// Runs a transactional action and maps the successful result.
    /// </summary>
    /// <typeparam name="TIn">Input success type.</typeparam>
    /// <typeparam name="TOut">Output success type.</typeparam>
    /// <param name="db">Database facade.</param>
    /// <param name="action">Transactional action.</param>
    /// <param name="map">Success mapper.</param>
    /// <param name="cancellationToken">Token used to cancel the transactional flow.</param>
    /// <returns>A mapped <see cref="Fin{T}"/> result.</returns>
    public static async Task<Fin<TOut>> InTransactionMapAsync<TIn, TOut>(
        this FunctionalMsSqlDb db,
        Func<FunctionalMsSqlDb, Task<Fin<TIn>>> action,
        Func<TIn, TOut> map,
        CancellationToken cancellationToken = default)
    {
        if (db is null)
        {
            return FinFail<TOut>(Error.New("Database facade cannot be null."));
        }

        if (action is null)
        {
            return FinFail<TOut>(Error.New("Transaction action cannot be null."));
        }

        if (map is null)
        {
            return FinFail<TOut>(Error.New("Map function cannot be null."));
        }

        var result = await db.InTransactionAsync(action, cancellationToken).ConfigureAwait(false);
        return result.Map(map);
    }
}
