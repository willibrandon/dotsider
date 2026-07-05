namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One managed assembly's contribution to a managed↔native correlation build: its simple
/// name (ILC embeds it in every mangled symbol) and its method definitions.
/// </summary>
/// <param name="AssemblyName">The assembly simple name, exactly as mstat records attribute it.</param>
/// <param name="Methods">The assembly's method definitions.</param>
public sealed record ManagedMethodSource(
    string AssemblyName,
    IReadOnlyList<MethodDefInfo> Methods);
