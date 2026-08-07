/// <summary>
/// Captures one completed deployment orchestration process.
/// Standard streams remain separate for concise command diagnostics.
/// Timeout and truncation state are retained from the shared script runner.
/// </summary>
internal sealed record DeploymentProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool StandardOutputTruncated,
    bool StandardErrorTruncated,
    bool TimedOut);
