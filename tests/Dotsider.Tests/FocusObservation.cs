using Hex1b;

namespace Dotsider.Tests;

/// <summary>
/// The focus ring as one root build observed it on the app run loop.
/// </summary>
/// <param name="FocusedNode">The focused node, or <see langword="null"/> when nothing has focus.</param>
/// <param name="Focusables">A copy of the focusable nodes in render order.</param>
internal sealed record FocusObservation(Hex1bNode? FocusedNode, IReadOnlyList<Hex1bNode> Focusables)
{
    /// <summary>The observation reported before the first root build.</summary>
    internal static FocusObservation None { get; } = new(null, []);
}
