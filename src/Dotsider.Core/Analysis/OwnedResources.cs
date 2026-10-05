namespace Dotsider.Core.Analysis;

/// <summary>Owns resources acquired incrementally during an operation.</summary>
/// <typeparam name="T">The type of resource collected by the operation.</typeparam>
internal sealed class OwnedResources<T> : IDisposable where T : class, IDisposable
{
    private readonly List<T> _resources = [];

    /// <summary>Registers a resource for cleanup.</summary>
    /// <typeparam name="TResource">The concrete resource type to preserve.</typeparam>
    /// <param name="resource">The resource whose ownership is transferred into this scope.</param>
    /// <returns>The registered resource.</returns>
    internal TResource Add<TResource>(TResource resource) where TResource : T
    {
        _resources.Add(resource);
        return resource;
    }

    /// <summary>Transfers all registered resources to the caller.</summary>
    internal void ReleaseAll() => _resources.Clear();

    /// <inheritdoc />
    public void Dispose()
    {
        // Remove before disposing so repeated cleanup cannot dispose a resource twice.
        // The finally ensures an individual failure cannot leak the remaining resources.
        try
        {
            while (_resources.Count > 0)
            {
                var index = _resources.Count - 1;
                var resource = _resources[index];
                _resources.RemoveAt(index);
                resource.Dispose();
            }
        }
        finally
        {
            if (_resources.Count > 0) Dispose();
        }
    }
}
