namespace ComplexApp.Pipeline;

/// <summary>
/// Pipeline step that trims leading and trailing whitespace.
/// </summary>
public sealed class TrimStep : IPipelineStep<string>
{
    /// <inheritdoc />
    public Task<string> ProcessAsync(string input, CancellationToken ct = default)
        => Task.FromResult(input.Trim());
}

/// <summary>
/// Pipeline step that converts the input to upper case.
/// </summary>
public sealed class UpperCaseStep : IPipelineStep<string>
{
    /// <inheritdoc />
    public Task<string> ProcessAsync(string input, CancellationToken ct = default)
        => Task.FromResult(input.ToUpperInvariant());
}

/// <summary>
/// Pipeline step that prepends a fixed prefix to the input.
/// </summary>
/// <param name="prefix">The string to prepend.</param>
public sealed class PrefixStep(string prefix) : IPipelineStep<string>
{
    /// <inheritdoc />
    public Task<string> ProcessAsync(string input, CancellationToken ct = default)
        => Task.FromResult($"{prefix}{input}");
}
