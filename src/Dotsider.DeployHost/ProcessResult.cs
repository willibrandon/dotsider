namespace Dotsider.DeployHost;

/// <summary>
/// Represents the bounded result of one external process invocation.
/// Standard output and error remain separate for precise diagnostics.
/// The exit code determines whether callers continue privileged work.
/// </summary>
internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
