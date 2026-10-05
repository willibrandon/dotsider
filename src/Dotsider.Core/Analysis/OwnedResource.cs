namespace Dotsider.Core.Analysis;

/// <summary>
/// Disposes a resource if an operation fails before transferring ownership to its caller.
/// </summary>
/// <typeparam name="T">The resource whose lifetime is being transferred.</typeparam>
internal sealed class OwnedResource<T>(T value) : IDisposable where T : class, IDisposable
{
    private T? _value = value;

    /// <summary>Gets the resource while this scope owns it.</summary>
    internal T Value => Volatile.Read(ref _value)
        ?? throw new ObjectDisposedException(nameof(OwnedResource<>));

    /// <summary>Transfers the resource to a new owner without disposing it.</summary>
    /// <returns>The resource that the caller must now dispose.</returns>
    internal T Release() => Interlocked.Exchange(ref _value, null)
        ?? throw new ObjectDisposedException(nameof(OwnedResource<>));

    /// <inheritdoc />
    public void Dispose() => Interlocked.Exchange(ref _value, null)?.Dispose();
}
