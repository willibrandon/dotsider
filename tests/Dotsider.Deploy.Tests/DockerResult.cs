namespace Dotsider.Deploy.Tests;

/// <summary>
/// Captures one completed Docker CLI invocation.
/// Standard output and error remain separate for precise integration diagnostics.
/// A nonzero exit code is interpreted by the fixture or individual test.
/// </summary>
internal sealed record DockerResult(int ExitCode, string StandardOutput, string StandardError);
