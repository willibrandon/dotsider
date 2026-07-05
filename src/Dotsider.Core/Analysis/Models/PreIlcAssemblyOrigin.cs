namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// How the pre-ILC managed input of a Native AOT binary was located, ordered from
/// most to least authoritative.
/// </summary>
public enum PreIlcAssemblyOrigin
{
    /// <summary>No managed input was found; the result may still carry mstat/DGML paths.</summary>
    None,

    /// <summary>
    /// Named as the root input of the ILC response file (<c>*.ilc.rsp</c>) — the exact
    /// file the compiler consumed.
    /// </summary>
    IlcResponseFile,

    /// <summary>
    /// Found at the SDK's conventional intermediate location for the recognized build
    /// tree (<c>obj\&lt;cfg&gt;\&lt;tfm&gt;\&lt;rid&gt;</c>, or the artifacts-layout equivalent).
    /// </summary>
    BuildTreeLayout,

    /// <summary>
    /// Found beside the binary itself — manual staging with no build provenance.
    /// </summary>
    SiblingAssembly,
}
