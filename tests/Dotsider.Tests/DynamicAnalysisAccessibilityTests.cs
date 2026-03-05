using Dotsider.Analysis.Models;
using Dotsider.Views;

namespace Dotsider.Tests;

public class DynamicAnalysisAccessibilityTests
{
    [Fact]
    public void CategoryColors_AllCategoriesHaveEntries()
    {
        foreach (var category in Enum.GetValues<TraceEventCategory>())
            Assert.True(DynamicAnalysisView.CategoryColors.ContainsKey(category),
                $"Missing color for {category}");
    }

    [Fact]
    public void CategoryColors_NoDuplicateColors()
    {
        var seen = new Dictionary<(byte R, byte G, byte B), TraceEventCategory>();
        foreach (var (category, color) in DynamicAnalysisView.CategoryColors)
        {
            // Counter and Other are allowed to share DimGray — they are
            // non-primary categories that never appear in the events table
            if (category is TraceEventCategory.Counter or TraceEventCategory.Other)
                continue;

            var key = (color.R, color.G, color.B);
            Assert.False(seen.TryGetValue(key, out var existing),
                $"{category} shares color ({color.R},{color.G},{color.B}) with {existing}");
            seen[key] = category;
        }
    }

    [Fact]
    public void SocketAndHttp_HaveDifferentColors()
    {
        var httpColor = DynamicAnalysisView.CategoryColors[TraceEventCategory.Http];
        var socketColor = DynamicAnalysisView.CategoryColors[TraceEventCategory.Socket];
        Assert.False(
            httpColor.R == socketColor.R && httpColor.G == socketColor.G && httpColor.B == socketColor.B,
            "Http and Socket must have distinct colors for accessibility");
    }
}
