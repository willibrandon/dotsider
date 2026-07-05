using Dotsider.Core.Analysis;

namespace Dotsider.Views;

/// <summary>
/// Decides which analyzer feeds each PE/Metadata sub-tab when a pre-ILC companion is
/// attached: metadata tables fill from the managed input while the binary tables stay on
/// the native output. Shared by the table builders, search-key collection, seed-focus,
/// and yank so every surface answers from the same analyzer.
/// </summary>
internal static class PeMetadataRouting
{
    /// <summary>
    /// The analyzer that feeds the given PE sub-tab. The Debug Directory is not listed:
    /// its table merges both analyzers' entries itself.
    /// </summary>
    /// <param name="state">The shared application state.</param>
    /// <param name="subTab">The PE sub-tab index.</param>
    internal static AssemblyAnalyzer AnalyzerForPeSubTab(DotsiderState state, int subTab) => subTab switch
    {
        PeSubTabId.TypeDef or PeSubTabId.MethodDef or PeSubTabId.TypeRef or PeSubTabId.MemberRef
            or PeSubTabId.Attributes or PeSubTabId.Resources => state.MetadataAnalyzer,
        _ => state.Analyzer,
    };
}
