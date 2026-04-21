namespace Dotsider.Core.Analysis;

/// <summary>
/// Formats an assembly's full identity into a stable opaque string used as a graph node
/// identifier and as a key for grouping <see cref="Models.TypeRefInfo"/> entries by the
/// full identity of their resolution scope.
/// </summary>
/// <remarks>
/// The format is <c>"{Name}|{Version}|{Culture}|{PublicKeyToken}"</c>. Null or empty culture
/// is normalized to <c>"neutral"</c> so two nodes only differ by culture when they truly do.
/// The identifier is treated as opaque by consumers; it is never parsed.
/// </remarks>
public static class AssemblyIdentityFormat
{
    /// <summary>
    /// Formats an assembly identity into its canonical identifier string.
    /// </summary>
    /// <param name="name">The assembly simple name.</param>
    /// <param name="version">The assembly version, or <see langword="null"/>.</param>
    /// <param name="culture">The assembly culture, or <see langword="null"/>/empty for culture-neutral.</param>
    /// <param name="publicKeyToken">The public key token hex, or <see langword="null"/>.</param>
    /// <returns>A stable opaque identifier derived from the four identity fields.</returns>
    public static string Format(string name, string? version, string? culture, string? publicKeyToken)
    {
        var normalizedCulture = string.IsNullOrEmpty(culture) ? "neutral" : culture;
        return $"{name}|{version ?? string.Empty}|{normalizedCulture}|{publicKeyToken ?? string.Empty}";
    }
}
