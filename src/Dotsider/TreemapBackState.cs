using Dotsider.Core.Analysis.Models;

namespace Dotsider;

/// <summary>
/// Snapshot of the Size Map navigation state, captured into <see cref="IlBackEntry"/>
/// when a cross-assembly gd push is about to clear it via <c>ResetViewState</c>.
/// Restored on the cross-assembly branch of <c>RestoreFromIlBackEntry</c>.
/// </summary>
/// <param name="CurrentLevel">The treemap node currently being shown, or null at root.</param>
/// <param name="BreadcrumbTopFirst">Snapshot of the breadcrumb stack, top-of-stack first (matches <c>Stack&lt;T&gt;.ToArray</c>).</param>
/// <param name="SelectedIndex">Keyboard-selected child index within <see cref="CurrentLevel"/>, or -1.</param>
/// <param name="MatchIndex">Search-match index within <see cref="CurrentLevel"/>, or -1.</param>
/// <param name="CachedTree">The full <c>CachedSizeTree</c>, kept so restored node references stay identity-consistent with the original navigation graph.</param>
/// <param name="SearchQuery">The Size Map search query text, or null.</param>
/// <param name="SearchIsActive">Whether the Size Map search was active.</param>
/// <param name="SearchIsConfirmed">Whether the Size Map search was confirmed.</param>
/// <param name="SearchMatchCount">The Size Map search match count, or -1.</param>
public sealed record TreemapBackState(
    SizeNode? CurrentLevel,
    IReadOnlyList<SizeNode> BreadcrumbTopFirst,
    int SelectedIndex,
    int MatchIndex,
    SizeNode? CachedTree,
    string? SearchQuery,
    bool SearchIsActive,
    bool SearchIsConfirmed,
    int SearchMatchCount);
