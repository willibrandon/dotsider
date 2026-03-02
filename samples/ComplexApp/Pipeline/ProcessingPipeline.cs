namespace ComplexApp.Pipeline;

/// <summary>
/// A composable processing pipeline that chains steps.
/// </summary>
public sealed class ProcessingPipeline<T>
{
    private readonly List<IPipelineStep<T>> _steps = [];

    public void AddStep(IPipelineStep<T> step) => _steps.Add(step);

    public async Task<T> ExecuteAsync(T input, CancellationToken ct = default)
    {
        var current = input;
        foreach (var step in _steps)
        {
            ct.ThrowIfCancellationRequested();
            current = await step.ProcessAsync(current, ct);
        }
        return current;
    }
}
