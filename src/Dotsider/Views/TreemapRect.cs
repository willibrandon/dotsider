using Dotsider.Core.Analysis.Models;

namespace Dotsider.Views;

/// <summary>
/// A positioned rectangle in the treemap layout.
/// </summary>
/// <param name="X">The left edge of the rectangle.</param>
/// <param name="Y">The top edge of the rectangle.</param>
/// <param name="Width">The width of the rectangle.</param>
/// <param name="Height">The height of the rectangle.</param>
/// <param name="Node">The size node this rectangle represents.</param>
public sealed record TreemapRect(double X, double Y, double Width, double Height, SizeNode Node);
