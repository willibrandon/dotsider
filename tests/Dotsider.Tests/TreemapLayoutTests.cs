using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class TreemapLayoutTests(SampleAssemblyFixture samples)
{
    [Fact(Timeout = 30_000)]
    public void Layout_ProducesRectsWithinBounds()
    {
        var nodes = CreateTestNodes(5);
        var rects = TreemapLayout.Layout(nodes, 0, 0, 100, 100);
        Assert.NotEmpty(rects);
        foreach (var rect in rects)
        {
            Assert.True(rect.X >= -0.01);
            Assert.True(rect.Y >= -0.01);
            Assert.True(rect.X + rect.Width <= 100.01);
            Assert.True(rect.Y + rect.Height <= 100.01);
        }
    }

    [Fact(Timeout = 30_000)]
    public void Layout_NoNegativeDimensions()
    {
        var nodes = CreateTestNodes(10);
        var rects = TreemapLayout.Layout(nodes, 0, 0, 200, 100);
        Assert.All(rects, r =>
        {
            Assert.True(r.Width >= 0);
            Assert.True(r.Height >= 0);
        });
    }

    [Fact(Timeout = 30_000)]
    public void Layout_NoOverlappingRects()
    {
        var nodes = CreateTestNodes(8);
        var rects = TreemapLayout.Layout(nodes, 0, 0, 100, 100);
        AssertNoOverlaps(rects);
    }

    [Fact(Timeout = 30_000)]
    public void Layout_SingleNode_FillsEntireSpace()
    {
        var nodes = new List<SizeNode>
        {
            new("single", "single", 100, SizeNodeKind.Type, [])
        };
        var rects = TreemapLayout.Layout(nodes, 0, 0, 50, 30);
        Assert.Single(rects);
        Assert.Equal(0, rects[0].X, 0.01);
        Assert.Equal(0, rects[0].Y, 0.01);
        Assert.Equal(50, rects[0].Width, 0.01);
        Assert.Equal(30, rects[0].Height, 0.01);
    }

    [Fact(Timeout = 30_000)]
    public void Layout_EmptyInput_ReturnsEmpty()
    {
        var rects = TreemapLayout.Layout([], 0, 0, 100, 100);
        Assert.Empty(rects);
    }

    [Fact(Timeout = 30_000)]
    public void Layout_ZeroWidthOrHeight_ReturnsEmpty()
    {
        var nodes = CreateTestNodes(3);
        Assert.Empty(TreemapLayout.Layout(nodes, 0, 0, 0, 100));
        Assert.Empty(TreemapLayout.Layout(nodes, 0, 0, 100, 0));
    }

    [Fact(Timeout = 30_000)]
    public void Layout_RealSizeTree_ProducesValidRects()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var rects = TreemapLayout.Layout(tree.Children, 0, 0, 800, 600);
        Assert.NotEmpty(rects);
        Assert.All(rects, r =>
        {
            Assert.True(r.Width >= 0);
            Assert.True(r.Height >= 0);
        });
    }

    [Fact(Timeout = 30_000)]
    public void Layout_ManySmallNodes_AllFitWithinBounds()
    {
        var nodes = CreateTestNodes(20);
        var rects = TreemapLayout.Layout(nodes, 10, 10, 200, 150);
        Assert.NotEmpty(rects);
        foreach (var rect in rects)
        {
            Assert.True(rect.X >= 9.99);
            Assert.True(rect.Y >= 9.99);
            Assert.True(rect.X + rect.Width <= 210.01);
            Assert.True(rect.Y + rect.Height <= 160.01);
        }
    }

    [Fact(Timeout = 30_000)]
    public void Layout_NoOverlappingRects_RealAssembly()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var rects = TreemapLayout.Layout(tree.Children, 0, 0, 800, 600);
        Assert.NotEmpty(rects);
        AssertNoOverlaps(rects);
    }

    [Fact(Timeout = 30_000)]
    public void AssertNoOverlaps_DetectsKnownOverlap()
    {
        var node = new SizeNode("test", "test", 100, SizeNodeKind.Type, []);
        var overlapping = new List<TreemapRect>
        {
            new(0, 0, 50, 50, node),
            new(25, 25, 50, 50, node),
        };
        var ex = Assert.ThrowsAny<Exception>(() => AssertNoOverlaps(overlapping));
        Assert.Contains("overlap", ex.Message);
    }

    [Fact(Timeout = 30_000)]
    public void AssertNoOverlaps_AllowsAdjacentRects()
    {
        var node = new SizeNode("test", "test", 100, SizeNodeKind.Type, []);
        var adjacent = new List<TreemapRect>
        {
            new(0, 0, 50, 50, node),
            new(50, 0, 50, 50, node),
        };
        AssertNoOverlaps(adjacent);
    }

    [Fact(Timeout = 30_000)]
    public void Layout_RectsMatchInputNodes()
    {
        var nodes = CreateTestNodes(5);
        var rects = TreemapLayout.Layout(nodes, 0, 0, 100, 100);
        Assert.Equal(nodes.Count, rects.Count);
    }

    /// <summary>
    /// Full geometry invariant for the RichLibrary root at a standard viewport:
    /// total rect area matches container area, no overlaps, edges reach bounds.
    /// See: https://github.com/willibrandon/dotsider/issues/134
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Layout_RealAssembly_RectsCoverFullArea()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        const double width = 120;
        const double height = 30;
        var rects = TreemapLayout.Layout(tree.Children, 0, 0, width, height);

        Assert.NotEmpty(rects);
        AssertNoOverlaps(rects);
        AssertFullCoverage(rects, width, height);
    }

    /// <summary>
    /// Same geometry invariant at a larger viewport to cover different aspect ratios
    /// and squarification decisions.
    /// See: https://github.com/willibrandon/dotsider/issues/134
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Layout_RealAssembly_RectsCoverFullArea_LargeViewport()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        const double width = 240;
        const double height = 60;
        var rects = TreemapLayout.Layout(tree.Children, 0, 0, width, height);

        Assert.NotEmpty(rects);
        AssertNoOverlaps(rects);
        AssertFullCoverage(rects, width, height);
    }

    /// <summary>
    /// Geometry invariant for a drilled namespace with 3+ type children.
    /// Verifies the layout fix works at subtree levels, not just the root.
    /// See: https://github.com/willibrandon/dotsider/issues/134
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Layout_RealAssembly_DrilledNamespace_RectsCoverFullArea()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        // Find the first namespace with 3+ children for a meaningful drilled layout
        var ns = tree.Children.FirstOrDefault(c =>
            c.Kind == SizeNodeKind.Namespace && c.Children.Count >= 3);
        Assert.NotNull(ns);

        const double width = 120;
        const double height = 30;
        var rects = TreemapLayout.Layout(ns.Children, 0, 0, width, height);

        Assert.NotEmpty(rects);
        AssertNoOverlaps(rects);
        AssertFullCoverage(rects, width, height);
    }

    private static List<SizeNode> CreateTestNodes(int count)
    {
        var nodes = new List<SizeNode>();
        for (var i = 0; i < count; i++)
        {
            nodes.Add(new SizeNode($"node{i}", $"node{i}", (i + 1) * 10, SizeNodeKind.Type, []));
        }
        return nodes;
    }

    private static void AssertFullCoverage(IReadOnlyList<TreemapRect> rects, double width, double height)
    {
        var totalRectArea = rects.Sum(r => r.Width * r.Height);
        Assert.Equal(width * height, totalRectArea, 0.01);

        var maxRight = rects.Max(r => r.X + r.Width);
        Assert.Equal(width, maxRight, 0.01);

        var maxBottom = rects.Max(r => r.Y + r.Height);
        Assert.Equal(height, maxBottom, 0.01);
    }

    private static void AssertNoOverlaps(IReadOnlyList<TreemapRect> rects)
    {
        const double tolerance = 0.1;
        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i];
                var b = rects[j];
                var overlaps =
                    a.X < b.X + b.Width - tolerance &&
                    a.X + a.Width > b.X + tolerance &&
                    a.Y < b.Y + b.Height - tolerance &&
                    a.Y + a.Height > b.Y + tolerance;
                if (overlaps)
                    Assert.Fail($"Rects {i} and {j} overlap: ({a.X:F1},{a.Y:F1},{a.Width:F1},{a.Height:F1}) vs ({b.X:F1},{b.Y:F1},{b.Width:F1},{b.Height:F1})");
            }
        }
    }
}
