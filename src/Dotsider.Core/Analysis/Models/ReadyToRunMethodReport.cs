namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The resolved ReadyToRun correlation for one method, shared verbatim by the CLI
/// <c>--r2r-correlate</c> option, the MCP <c>correlate_r2r_method</c> tool, and the session
/// <c>r2r-correlate</c> command. Carries both rendered text and structured instruction arrays so
/// programmatic callers get the IL and native code, not just formatted output.
/// </summary>
/// <param name="Availability">Why the method does or does not have inspectable native code.</param>
/// <param name="Assembly">The owning assembly's simple name.</param>
/// <param name="Mvid">The owning assembly's module version id (composite identity).</param>
/// <param name="Method">The method's display form: <c>DeclaringType::Name signature</c>.</param>
/// <param name="Token">The method's metadata token.</param>
/// <param name="IsComposite">Whether the image is composite.</param>
/// <param name="OwnerComponent">The owning component assembly for a composite, else null.</param>
/// <param name="IsGenericInstantiation">Whether this entry is a generic instantiation.</param>
/// <param name="InstantiationDisplay">The rendered instantiation (e.g. <c>&lt;int&gt;</c>), or null.</param>
/// <param name="Ranges">One entry per native code range (hot entry, funclets, cold).</param>
/// <param name="NativeSize">The total precompiled native code size.</param>
/// <param name="Il">The method's IL listing text, or null when metadata is unavailable.</param>
/// <param name="IlInstructions">The structured IL instructions, or null.</param>
/// <param name="NativeText">The concatenated native disassembly across ranges, or null.</param>
/// <param name="NativeInstructions">The structured native instructions across ranges, or null.</param>
/// <param name="Diagnostic">A human-readable note for a non-<see cref="ReadyToRunNativeAvailability.Precompiled"/> availability, or null.</param>
public sealed record ReadyToRunMethodReport(
    ReadyToRunNativeAvailability Availability,
    string Assembly,
    Guid Mvid,
    string Method,
    int Token,
    bool IsComposite,
    string? OwnerComponent,
    bool IsGenericInstantiation,
    string? InstantiationDisplay,
    IReadOnlyList<CorrelationReportSymbol> Ranges,
    long NativeSize,
    string? Il,
    IReadOnlyList<IlInstruction>? IlInstructions,
    string? NativeText,
    IReadOnlyList<NativeInstruction>? NativeInstructions,
    string? Diagnostic);
