/// <summary>
/// Returns caller-selected results for deployment orchestration process calls.
/// Every executable and literal argument array is recorded in invocation order.
/// The stub does not access the network or start child processes.
/// </summary>
internal sealed class StubDeploymentProcessRunner(
    Func<string, string[], DeploymentProcessResult> resultFactory) : IDeploymentProcessRunner
{
    /// <summary>
    /// Gets every recorded process call in invocation order.
    /// Tuple arguments retain the executable and copied literal argument array.
    /// The collection is owned by the current test.
    /// </summary>
    internal List<(string FileName, string[] Arguments)> Calls { get; } = [];

    /// <inheritdoc />
    public DeploymentProcessResult Run(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        int maxOutputCharacters,
        TimeSpan timeout)
    {
        _ = workingDirectory;
        _ = maxOutputCharacters;
        _ = timeout;
        string[] argumentArray = [.. arguments];
        Calls.Add((fileName, argumentArray));
        return resultFactory(fileName, argumentArray);
    }
}
