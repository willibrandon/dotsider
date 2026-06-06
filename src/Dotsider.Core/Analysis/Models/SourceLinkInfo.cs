namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Source Link mappings decoded from portable PDB custom debug information.
/// </summary>
/// <param name="Mappings">The document pattern to URL template mappings.</param>
public sealed record SourceLinkInfo(IReadOnlyList<SourceLinkMapping> Mappings)
{
    /// <summary>Gets whether Source Link data was present.</summary>
    public bool IsPresent => Mappings.Count > 0;
}
