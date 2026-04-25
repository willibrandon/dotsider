namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One <c>&lt;codeBase&gt;</c> entry parsed from a .NET Framework configuration file or
/// publisher-policy assembly. CodeBase entries are honored only for strong-named binds at
/// the version specified.
/// </summary>
/// <param name="Source">Which policy layer this codeBase came from.</param>
/// <param name="Name">Simple name of the assembly.</param>
/// <param name="PublicKeyToken">Hex-string PKT.</param>
/// <param name="Culture">Culture, defaulting to <c>"neutral"</c>.</param>
/// <param name="Version">The version this codeBase is anchored to.</param>
/// <param name="Href">
/// The configured <c>href</c>, either an absolute path/URL or a path relative to the
/// application base.
/// </param>
public sealed record CodeBaseEntry(
    PolicyLayer Source,
    string Name,
    string? PublicKeyToken,
    string Culture,
    Version Version,
    string Href);
