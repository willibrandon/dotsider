namespace Dotsider.Views;

/// <summary>
/// Flags indicating which identity fields differ among graph nodes sharing a simple name and
/// therefore need to appear in disambiguated labels. Computed per render over the visible
/// nodes so that the disambiguator always reflects what the user actually sees.
/// </summary>
/// <param name="IncludeVersion">The version component differs among colliding nodes.</param>
/// <param name="IncludeCulture">The culture component differs among colliding nodes.</param>
/// <param name="IncludePkt">The public key token differs among colliding nodes.</param>
internal sealed record IdentityDiscriminator(
    bool IncludeVersion,
    bool IncludeCulture,
    bool IncludePkt);
