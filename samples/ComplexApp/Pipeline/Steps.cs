namespace ComplexApp.Pipeline;

public sealed class TrimStep : IPipelineStep<string>
{
    public Task<string> ProcessAsync(string input, CancellationToken ct = default)
        => Task.FromResult(input.Trim());
}

public sealed class UpperCaseStep : IPipelineStep<string>
{
    public Task<string> ProcessAsync(string input, CancellationToken ct = default)
        => Task.FromResult(input.ToUpperInvariant());
}

public sealed class PrefixStep(string prefix) : IPipelineStep<string>
{
    public Task<string> ProcessAsync(string input, CancellationToken ct = default)
        => Task.FromResult($"{prefix}{input}");
}
