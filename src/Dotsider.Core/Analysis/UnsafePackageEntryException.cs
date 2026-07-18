namespace Dotsider.Core.Analysis;

/// <summary>
/// The exception that is thrown when a package entry cannot be safely extracted beneath its
/// destination directory.
/// </summary>
public sealed class UnsafePackageEntryException : IOException
{
    private const string DefaultMessage =
        "The package entry cannot be extracted because its path is unsafe or ambiguous.";

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsafePackageEntryException"/> class.
    /// </summary>
    public UnsafePackageEntryException()
        : base(DefaultMessage)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsafePackageEntryException"/> class with a
    /// specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public UnsafePackageEntryException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnsafePackageEntryException"/> class with a
    /// specified error message and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">
    /// The exception that caused the current exception, or <see langword="null"/>.
    /// </param>
    public UnsafePackageEntryException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
