namespace Dotsider.Infrastructure;

/// <summary>
/// A live dotsider session discovered by the CLI.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliDiscoveredSessionPayload(
    int Pid,
    string SocketPath,
    string Mode,
    string FileName,
    string AssemblyName);
