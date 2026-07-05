namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One pre-ILC managed method joined to its native evidence: the symbols that carry its
/// compiled code and the mstat rows that carry its sizes.
/// </summary>
/// <param name="AssemblyName">The simple name of the assembly the method is defined in.</param>
/// <param name="Method">The managed method definition.</param>
/// <param name="Status">How the method relates to the native image.</param>
/// <param name="NativeSymbols">The joined native symbols — owned when exact (several mean generic instantiations), the shared candidate pool when ambiguous.</param>
/// <param name="MstatMethods">The joined mstat rows, empty when no mstat sidecar was available.</param>
public sealed record MethodCorrelation(
    string AssemblyName,
    MethodDefInfo Method,
    MethodCorrelationStatus Status,
    IReadOnlyList<NativeSymbol> NativeSymbols,
    IReadOnlyList<MstatMethod> MstatMethods)
{
    /// <summary>
    /// The native size in bytes this method owns outright — mstat sizes preferred, symbol
    /// sizes otherwise. Zero when the evidence is shared with overloads: shared bytes are
    /// never attributed to any single candidate.
    /// </summary>
    public long NativeSize { get; init; }

    /// <summary>
    /// The size of the shared evidence pool this method is a candidate for, when
    /// <see cref="Status"/> reflects shared evidence. The same pool is reported on every
    /// sibling candidate; aggregate accounting counts it once.
    /// </summary>
    public long SharedCandidateSize { get; init; }
}
