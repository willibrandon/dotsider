using Dotsider.Core.Analysis.Models;

namespace Dotsider.Views;

/// <summary>
/// Squarified treemap layout algorithm.
/// Produces rectangles with aspect ratios close to 1:1 for better readability.
/// </summary>
public static class TreemapLayout
{
    /// <summary>
    /// Computes a squarified treemap layout for the given nodes within the specified bounds.
    /// </summary>
    /// <param name="nodes">The size nodes to lay out.</param>
    /// <param name="x">The left edge of the layout area.</param>
    /// <param name="y">The top edge of the layout area.</param>
    /// <param name="width">The width of the layout area.</param>
    /// <param name="height">The height of the layout area.</param>
    /// <returns>A list of positioned rectangles for each node.</returns>
    public static IReadOnlyList<TreemapRect> Layout(
        IReadOnlyList<SizeNode> nodes, double x, double y, double width, double height)
    {
        if (nodes.Count == 0 || width <= 0 || height <= 0)
            return [];

        var totalSize = nodes.Sum(n => n.Size);
        if (totalSize <= 0) return [];

        // Sort by size descending for better squarification
        var sorted = nodes.OrderByDescending(n => n.Size).ToList();
        var result = new List<TreemapRect>();
        Squarify(sorted, 0, x, y, width, height, totalSize, result);
        return result;
    }

    private static void Squarify(
        List<SizeNode> items, int startIndex,
        double x, double y, double w, double h,
        double totalArea, List<TreemapRect> result)
    {
        if (startIndex >= items.Count || w <= 0 || h <= 0)
            return;

        if (items.Count - startIndex == 1)
        {
            result.Add(new TreemapRect(x, y, w, h, items[startIndex]));
            return;
        }

        // Determine layout direction
        var isHorizontal = w >= h;
        var shortSide = isHorizontal ? h : w;

        // Find the best row
        var rowItems = new List<int>();
        var rowArea = 0.0;
        var bestWorst = double.MaxValue;

        for (var i = startIndex; i < items.Count; i++)
        {
            var itemArea = (double)items[i].Size / totalArea * w * h;
            rowItems.Add(i);
            rowArea += itemArea;

            var worst = WorstAspect(rowItems, items, totalArea, w, h, shortSide, rowArea);
            if (worst <= bestWorst)
            {
                bestWorst = worst;
            }
            else
            {
                // Adding this item made it worse, remove it
                rowItems.RemoveAt(rowItems.Count - 1);
                rowArea -= itemArea;
                break;
            }
        }

        // Layout the row
        if (rowArea <= 0 || rowItems.Count == 0)
            return;

        var rowLength = rowArea / shortSide;
        var offset = 0.0;

        foreach (var idx in rowItems)
        {
            var itemArea = (double)items[idx].Size / totalArea * w * h;
            var itemLength = itemArea / rowLength;

            if (isHorizontal)
            {
                result.Add(new TreemapRect(x, y + offset, rowLength, itemLength, items[idx]));
                offset += itemLength;
            }
            else
            {
                result.Add(new TreemapRect(x + offset, y, itemLength, rowLength, items[idx]));
                offset += itemLength;
            }
        }

        // Recurse on remaining items in the reduced rectangle
        var nextIndex = rowItems[^1] + 1;
        if (nextIndex < items.Count)
        {
            if (isHorizontal)
                Squarify(items, nextIndex, x + rowLength, y, w - rowLength, h, totalArea, result);
            else
                Squarify(items, nextIndex, x, y + rowLength, w, h - rowLength, totalArea, result);
        }
    }

    private static double WorstAspect(
        List<int> row, List<SizeNode> items, double totalArea,
        double w, double h, double shortSide, double rowArea)
    {
        var rowLength = rowArea / shortSide;
        if (rowLength <= 0) return double.MaxValue;

        var worst = 0.0;
        foreach (var idx in row)
        {
            var itemArea = (double)items[idx].Size / totalArea * w * h;
            var itemLength = itemArea / rowLength;
            if (itemLength <= 0) continue;
            var aspect = Math.Max(rowLength / itemLength, itemLength / rowLength);
            worst = Math.Max(worst, aspect);
        }
        return worst;
    }
}
