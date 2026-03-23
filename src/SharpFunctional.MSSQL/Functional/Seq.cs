using System.Collections;
using System.Collections.Immutable;

namespace SharpFunctional.MsSql.Functional;

/// <summary>
/// An immutable sequence wrapper providing functional-style collection semantics.
/// This is a lightweight, zero-dependency alternative to <c>LanguageExt.Seq&lt;T&gt;</c>.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public readonly struct Seq<T> : IReadOnlyList<T>, IEquatable<Seq<T>>
{
    private readonly ImmutableArray<T> _items;

    /// <summary>
    /// Creates a <see cref="Seq{T}"/> from an existing <see cref="ImmutableArray{T}"/>.
    /// </summary>
    /// <param name="items">The backing immutable array.</param>
    public Seq(ImmutableArray<T> items)
    {
        _items = items.IsDefault ? [] : items;
    }

    /// <summary>
    /// Creates a <see cref="Seq{T}"/> from an enumerable.
    /// </summary>
    /// <param name="items">The source items.</param>
    public Seq(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = [.. items];
    }

    private ImmutableArray<T> Items => _items.IsDefault ? [] : _items;

    /// <summary>
    /// Gets an empty <see cref="Seq{T}"/>.
    /// </summary>
    public static Seq<T> Empty => default;

    /// <summary>
    /// Gets the number of elements in this sequence.
    /// </summary>
    public int Count => Items.Length;

    /// <summary>
    /// Gets whether this sequence is empty.
    /// </summary>
    public bool IsEmpty => Items.IsEmpty;

    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The element at the given index.</returns>
    public T this[int index] => Items[index];

    /// <summary>
    /// Transforms each element of this sequence using <paramref name="map"/>.
    /// </summary>
    /// <typeparam name="TResult">The mapped element type.</typeparam>
    /// <param name="map">The mapping function.</param>
    /// <returns>A new sequence with mapped elements.</returns>
    public Seq<TResult> Map<TResult>(Func<T, TResult> map)
    {
        var builder = ImmutableArray.CreateBuilder<TResult>(Items.Length);
        foreach (var item in Items)
            builder.Add(map(item));
        return new Seq<TResult>(builder.MoveToImmutable());
    }

    /// <summary>
    /// Filters this sequence using <paramref name="predicate"/>.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>A new sequence with matching elements.</returns>
    public Seq<T> Filter(Func<T, bool> predicate)
    {
        var builder = ImmutableArray.CreateBuilder<T>();
        foreach (var item in Items)
        {
            if (predicate(item))
                builder.Add(item);
        }
        return new Seq<T>(builder.ToImmutable());
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{T}"/> over the underlying data.
    /// </summary>
    /// <returns>A span over the sequence elements.</returns>
    public ReadOnlySpan<T> AsSpan() => Items.AsSpan();

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)Items).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Converts this sequence to a <see cref="List{T}"/>.
    /// </summary>
    /// <returns>A mutable list copy.</returns>
    public List<T> ToList() => [.. Items];

    /// <summary>
    /// Converts this sequence to an array.
    /// </summary>
    /// <returns>An array copy.</returns>
    public T[] ToArray() => [.. Items];

    /// <summary>
    /// Implicit conversion from <see cref="ImmutableArray{T}"/> to <see cref="Seq{T}"/>.
    /// </summary>
    public static implicit operator Seq<T>(ImmutableArray<T> items) => new(items);

    /// <inheritdoc />
    public bool Equals(Seq<T> other)
    {
        if (Count != other.Count) return false;
        for (var i = 0; i < Count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(Items[i], other.Items[i]))
                return false;
        }
        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Seq<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Items)
            hash.Add(item);
        return hash.ToHashCode();
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Seq<T> left, Seq<T> right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Seq<T> left, Seq<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"Seq([{Count}])";
}
