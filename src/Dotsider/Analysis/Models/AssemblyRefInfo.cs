namespace Dotsider.Analysis.Models;

/// <summary>
/// Information about a referenced assembly from the AssemblyRef metadata table.
/// </summary>
/// <param name="Name">The simple name of the referenced assembly.</param>
/// <param name="Version">The version of the referenced assembly.</param>
/// <param name="Culture">The culture of the referenced assembly, or empty for culture-neutral.</param>
/// <param name="PublicKeyToken">The public key token as a hex string, or null if not strong-named.</param>
public sealed record AssemblyRefInfo(
    string Name,
    string Version,
    string Culture,
    string? PublicKeyToken);
