using SharpFunctional.MsSql.Functional;
using static SharpFunctional.MsSql.Functional.Prelude;

namespace SharpFunctional.MsSql.Common;

/// <summary>
/// Provides async functional composition helpers for <see cref="Option{T}"/>, <see cref="Seq{T}"/>, and <see cref="Fin{T}"/>.
/// </summary>
public static class FunctionalExtensions
{
    /// <summary>
    /// Binds an async optional value to another async optional value.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="source">The async optional source value.</param>
    /// <param name="binder">Function that maps the value to a new async optional.</param>
    /// <param name="cancellationToken">Token used to cancel awaiting the source or binding step.</param>
    public static async Task<Option<TOut>> Bind<TIn, TOut>(
        this Task<Option<TIn>> source,
        Func<TIn, Task<Option<TOut>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (source is null || binder is null)
        {
            return Option<TOut>.None;
        }

        try
        {
            var value = await source.WaitAsync(cancellationToken).ConfigureAwait(false);
            return await value.Match(
                    Some: async input => await binder(input).WaitAsync(cancellationToken).ConfigureAwait(false),
                    None: () => Task.FromResult(Option<TOut>.None))
                .ConfigureAwait(false);
        }
        catch
        {
            return Option<TOut>.None;
        }
    }

    /// <summary>
    /// Binds an async optional value to an async sequence.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="source">The async optional source value.</param>
    /// <param name="binder">Function that maps the value to an async sequence.</param>
    /// <param name="cancellationToken">Token used to cancel awaiting the source or binding step.</param>
    public static async Task<Seq<TOut>> Bind<TIn, TOut>(
        this Task<Option<TIn>> source,
        Func<TIn, Task<Seq<TOut>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (source is null || binder is null)
        {
            return Seq<TOut>();
        }

        try
        {
            var value = await source.WaitAsync(cancellationToken).ConfigureAwait(false);
            return await value.Match(
                    Some: async input => await binder(input).WaitAsync(cancellationToken).ConfigureAwait(false),
                    None: () => Task.FromResult(Seq<TOut>()))
                .ConfigureAwait(false);
        }
        catch
        {
            return Seq<TOut>();
        }
    }

    /// <summary>
    /// Maps an async sequence.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="source">The async source sequence.</param>
    /// <param name="mapper">Function that transforms the sequence.</param>
    /// <param name="cancellationToken">Token used to cancel awaiting the source sequence.</param>
    public static async Task<Seq<TOut>> Map<TIn, TOut>(
        this Task<Seq<TIn>> source,
        Func<Seq<TIn>, Seq<TOut>> mapper,
        CancellationToken cancellationToken = default)
    {
        if (source is null || mapper is null)
        {
            return Seq<TOut>();
        }

        try
        {
            var sequence = await source.WaitAsync(cancellationToken).ConfigureAwait(false);
            return mapper(sequence);
        }
        catch
        {
            return Seq<TOut>();
        }
    }

    /// <summary>
    /// Binds an async <see cref="Fin{T}"/> result.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="source">The async source result.</param>
    /// <param name="binder">Function that maps the successful value to a new async result.</param>
    /// <param name="cancellationToken">Token used to cancel awaiting the source or binding step.</param>
    public static async Task<Fin<TOut>> Bind<TIn, TOut>(
        this Task<Fin<TIn>> source,
        Func<TIn, Task<Fin<TOut>>> binder,
        CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            return FinFail<TOut>(Error.New("Source task cannot be null."));
        }

        if (binder is null)
        {
            return FinFail<TOut>(Error.New("Binder function cannot be null."));
        }

        try
        {
            var value = await source.WaitAsync(cancellationToken).ConfigureAwait(false);
            return await value.Match(
                    Succ: async input => await binder(input).WaitAsync(cancellationToken).ConfigureAwait(false),
                    Fail: error => Task.FromResult(FinFail<TOut>(error)))
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return FinFail<TOut>(Error.New(exception));
        }
    }
}
