namespace SharpFunctional.MsSql.Functional;

/// <summary>
/// Represents a void-equivalent value type for functional return signatures.
/// Use <see cref="Prelude.unit"/> to obtain the singleton instance.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>
{
    /// <summary>
    /// The singleton <see cref="Unit"/> value.
    /// </summary>
    public static readonly Unit Default = default;

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc />
    public override string ToString() => "()";

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Unit left, Unit right) => false;
}
