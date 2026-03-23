using System.Diagnostics.CodeAnalysis;

namespace SharpFunctional.MsSql.Functional;

/// <summary>
/// Represents an error with a human-readable message and an optional inner exception.
/// </summary>
public sealed class Error : IEquatable<Error>
{
    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional inner exception that caused this error.
    /// </summary>
    public Exception? Exception { get; }

    private Error(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }

    /// <summary>
    /// Creates a new <see cref="Error"/> from a message string.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A new <see cref="Error"/> instance.</returns>
    public static Error New(string message) => new(message);

    /// <summary>
    /// Creates a new <see cref="Error"/> from an exception, using its message.
    /// </summary>
    /// <param name="exception">The exception that caused the error.</param>
    /// <returns>A new <see cref="Error"/> instance.</returns>
    public static Error New(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new(exception.Message, exception);
    }

    /// <summary>
    /// Creates a new <see cref="Error"/> from a message and an exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exception">The exception that caused the error.</param>
    /// <returns>A new <see cref="Error"/> instance.</returns>
    public static Error New(string message, Exception exception) => new(message, exception);

    /// <inheritdoc />
    public bool Equals(Error? other) =>
        other is not null && Message == other.Message;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Error);

    /// <inheritdoc />
    public override int GetHashCode() => Message.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() =>
        Exception is not null
            ? $"{Message} ({Exception.GetType().Name})"
            : Message;

    /// <summary>
    /// Implicit conversion from <see cref="string"/> to <see cref="Error"/>.
    /// </summary>
    [return: NotNull]
    public static implicit operator Error(string message) => New(message);

    /// <summary>
    /// Implicit conversion from <see cref="System.Exception"/> to <see cref="Error"/>.
    /// </summary>
    [return: NotNull]
    public static implicit operator Error(Exception exception) => New(exception);
}
