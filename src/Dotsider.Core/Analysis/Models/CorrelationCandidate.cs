namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One of several methods a name query matched. Overloads share a name, so an ambiguous
/// query surfaces every candidate rather than guessing which the caller meant.
/// </summary>
/// <param name="AssemblyName">The simple name of the assembly the method is defined in.</param>
/// <param name="DeclaringType">The fully qualified declaring type name.</param>
/// <param name="Name">The method's simple name.</param>
/// <param name="Token">The method's metadata token.</param>
/// <param name="VirtualAddress">The first correlated native address, or null when the method is not in the native image.</param>
public sealed record CorrelationCandidate(
    string AssemblyName,
    string DeclaringType,
    string Name,
    int Token,
    ulong? VirtualAddress);
