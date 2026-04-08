namespace Dotsider.Core.Protocol;

/// <summary>
/// Result of resolving an assembly from the system .NET shared framework.
/// Includes the full path and the runtime pack that provided it.
/// </summary>
/// <param name="Path">Full path to the resolved assembly file.</param>
/// <param name="RuntimePack">The shared framework pack the assembly was found in (e.g. "Microsoft.NETCore.App").</param>
public sealed record FrameworkAssemblyInfo(
    string Path,
    string RuntimePack);
