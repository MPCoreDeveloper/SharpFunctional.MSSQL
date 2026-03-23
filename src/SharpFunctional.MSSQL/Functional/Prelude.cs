namespace SharpFunctional.MsSql.Functional;

/// <summary>
/// Provides static helper methods and constants for functional programming patterns.
/// Designed to be used with <c>using static SharpFunctional.MsSql.Functional.Prelude;</c>.
/// </summary>
public static class Prelude
{
    /// <summary>
    /// The singleton <see cref="Functional.Unit"/> value.
    /// </summary>
    public static readonly Unit unit = Unit.Default;

    /// <summary>
    /// Creates a <see cref="Fin{T}"/> failure from an <see cref="Error"/>.
    /// </summary>
    /// <typeparam name="T">The success type.</typeparam>
    /// <param name="error">The error value.</param>
    /// <returns>A failed <see cref="Fin{T}"/>.</returns>
    public static Fin<T> FinFail<T>(Error error) => Fin<T>.Fail(error);

    /// <summary>
    /// Creates a <see cref="Fin{T}"/> success from a value.
    /// </summary>
    /// <typeparam name="T">The success type.</typeparam>
    /// <param name="value">The success value.</param>
    /// <returns>A successful <see cref="Fin{T}"/>.</returns>
    public static Fin<T> FinSucc<T>(T value) => Fin<T>.Succ(value);

    /// <summary>
    /// Converts an <see cref="IEnumerable{T}"/> to a <see cref="Seq{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The source enumerable.</param>
    /// <returns>An immutable <see cref="Seq{T}"/>.</returns>
    public static Seq<T> toSeq<T>(IEnumerable<T> items) => new(items);

    /// <summary>
    /// Creates an empty <see cref="Seq{T}"/>.
    /// Used as <c>Seq&lt;T&gt;()</c> via <c>using static Prelude</c>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>An empty <see cref="Seq{T}"/>.</returns>
    public static Seq<T> Seq<T>() => Functional.Seq<T>.Empty;

    /// <summary>
    /// Creates a <see cref="Option{T}"/> containing <paramref name="value"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to wrap.</param>
    /// <returns>An <see cref="Option{T}"/> of <c>Some(value)</c>.</returns>
    public static Option<T> Some<T>(T value) => Option<T>.Some(value);

    /// <summary>
    /// Converts a nullable reference to an <see cref="Option{T}"/>.
    /// Returns <c>Some(value)</c> if non-null, <c>None</c> otherwise.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The nullable value.</param>
    /// <returns>An <see cref="Option{T}"/>.</returns>
    public static Option<T> Optional<T>(T? value) =>
        value is not null ? Option<T>.Some(value) : Option<T>.None;

    /// <summary>
    /// Returns <see cref="Option{T}.None"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>An empty <see cref="Option{T}"/>.</returns>
    public static Option<T> None<T>() => Option<T>.None;
}
