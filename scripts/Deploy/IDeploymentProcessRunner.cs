/// <summary>
/// Runs one local deployment orchestration process with literal arguments.
/// The abstraction allows failure recovery to be tested without an SSH server.
/// Implementations must not enable shell command parsing.
/// </summary>
internal interface IDeploymentProcessRunner
{
    /// <summary>
    /// Runs the requested process and captures bounded output.
    /// Environment-specific process details remain inside the implementation.
    /// Completion returns a result even when the process exits unsuccessfully.
    /// </summary>
    /// <param name="fileName">The executable name.</param>
    /// <param name="arguments">The literal process arguments.</param>
    /// <param name="workingDirectory">The process working directory.</param>
    /// <param name="maxOutputCharacters">The maximum retained characters per stream.</param>
    /// <param name="timeout">The process timeout.</param>
    /// <returns>The completed process result.</returns>
    DeploymentProcessResult Run(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        int maxOutputCharacters,
        TimeSpan timeout);
}
