using Dotsider.DeployHost;

namespace Dotsider.Deploy.Tests;

/// <summary>
/// Records deployment-host process calls without starting operating-system processes.
/// Tests may enqueue exact results to exercise success and failure paths.
/// Literal argument arrays remain available for boundary assertions.
/// </summary>
internal sealed class RecordingProcessRunner : IProcessRunner
{
    private readonly Queue<ProcessResult> _results = new();

    /// <summary>
    /// Gets the process calls recorded in invocation order.
    /// Every entry contains the executable followed by its literal arguments.
    /// Callers receive the mutable test-owned collection.
    /// </summary>
    internal List<string[]> Calls { get; } = [];

    /// <summary>
    /// Adds a result that will be returned by the next process call.
    /// An empty queue returns a successful result with empty output.
    /// Results are consumed in first-in, first-out order.
    /// </summary>
    /// <param name="result">The result to enqueue.</param>
    internal void Enqueue(ProcessResult result)
    {
        _results.Enqueue(result);
    }

    /// <inheritdoc />
    public Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add([fileName, .. arguments]);
        return Task.FromResult(
            _results.TryDequeue(out ProcessResult? result)
                ? result
                : new ProcessResult(0, string.Empty, string.Empty));
    }
}
