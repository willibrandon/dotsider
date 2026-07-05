namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The resolved correlation payload shared verbatim by every programmatic surface — the CLI
/// <c>--correlate</c> option, the session <c>correlate-method</c> command, and the MCP
/// <c>correlate_method</c> tool — so a method's pre-ILC IL and its native code are reported
/// identically wherever they are requested.
/// </summary>
/// <param name="Status">The correlation status name (exact, ambiguous, size-only, not-in-image).</param>
/// <param name="Assembly">The simple name of the assembly the method is defined in.</param>
/// <param name="Method">The method's display form: <c>DeclaringType::Name signature</c>.</param>
/// <param name="Token">The method's metadata token.</param>
/// <param name="Symbols">The native symbols carrying the method's code — several mean generic instantiations, or a shared overload pool when ambiguous.</param>
/// <param name="NativeSize">The native bytes the method owns outright, or 0 when its evidence is shared.</param>
/// <param name="SharedCandidateSize">The size of the shared evidence pool when the correlation is ambiguous, otherwise 0.</param>
/// <param name="MstatSize">The total mstat-reported native size, or 0 when no mstat sidecar was available.</param>
/// <param name="Il">The method's IL listing from the pre-ILC assembly, or null when it has no metadata body.</param>
/// <param name="NativeDisassembly">The correlation-aware native disassembly across all symbols, or null when no symbol is disassemblable.</param>
public sealed record CorrelationReport(
    string Status,
    string Assembly,
    string Method,
    int Token,
    IReadOnlyList<CorrelationReportSymbol> Symbols,
    long NativeSize,
    long SharedCandidateSize,
    long MstatSize,
    string? Il,
    string? NativeDisassembly);
