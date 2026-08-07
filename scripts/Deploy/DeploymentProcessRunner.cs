/// <summary>
/// Adapts the shared file-app process helper for deployment orchestration.
/// Processes receive literal argument boundaries and bounded output capture.
/// No command is passed through a shell.
/// </summary>
internal sealed class DeploymentProcessRunner : IDeploymentProcessRunner
{
    /// <inheritdoc />
    public DeploymentProcessResult Run(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        int maxOutputCharacters,
        TimeSpan timeout)
    {
        (int exitCode, string stdout, string stderr, bool stdoutTruncated, bool stderrTruncated, bool timedOut) =
            ScriptSupport.RunProcess(
                fileName,
                arguments,
                workingDirectory,
                maxOutputCharacters,
                timeout);
        return new DeploymentProcessResult(
            exitCode,
            stdout,
            stderr,
            stdoutTruncated,
            stderrTruncated,
            timedOut);
    }
}
