namespace ComplexApp.Pipeline;

/// <summary>
/// A single step in a processing pipeline.
/// </summary>
public interface IPipelineStep<T>
{
    /// <summary>
    /// Processes a single input and returns the transformed result.
    /// </summary>
    Task<T> ProcessAsync(T input, CancellationToken ct = default);
}
