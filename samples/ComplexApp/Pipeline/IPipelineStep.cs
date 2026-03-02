namespace ComplexApp.Pipeline;

/// <summary>
/// A single step in a processing pipeline.
/// </summary>
public interface IPipelineStep<T>
{
    Task<T> ProcessAsync(T input, CancellationToken ct = default);
}
