namespace Dotsider.DeployHost;

/// <summary>
/// Executes fixed programs with literal argument boundaries.
/// The abstraction permits deterministic failure testing without a shell.
/// Implementations must honor cancellation and return bounded output.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>
    /// Runs one executable with the supplied literal arguments.
    /// Shell parsing is never enabled for deployment operations.
    /// A completed process returns its exit code and captured streams.
    /// </summary>
    /// <param name="fileName">The executable name or absolute path.</param>
    /// <param name="arguments">The arguments passed without concatenation.</param>
    /// <param name="cancellationToken">Stops the running process.</param>
    /// <returns>The completed process result.</returns>
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
