namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One <c>&lt;bindingRedirect&gt;</c> entry parsed from a .NET Framework configuration file
/// or a publisher-policy assembly's embedded XML resource.
/// </summary>
/// <param name="Source">Which policy layer this redirect came from.</param>
/// <param name="Name">Simple name of the redirected assembly.</param>
/// <param name="PublicKeyToken">Hex-string PKT, lower-cased; <see langword="null"/> for weak-named.</param>
/// <param name="Culture">Culture, defaulting to <c>"neutral"</c>.</param>
/// <param name="ProcessorArchitecture">
/// <c>processorArchitecture</c> attribute on <c>&lt;assemblyIdentity&gt;</c>, or
/// <see langword="null"/> when unspecified (applies to any architecture).
/// </param>
/// <param name="OldMin">Inclusive lower bound of the redirected range.</param>
/// <param name="OldMax">Inclusive upper bound of the redirected range.</param>
/// <param name="NewVersion">The version the binder will use instead.</param>
public sealed record BindingRedirect(
    PolicyLayer Source,
    string Name,
    string? PublicKeyToken,
    string Culture,
    string? ProcessorArchitecture,
    Version OldMin,
    Version OldMax,
    Version NewVersion);
