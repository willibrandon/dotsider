using Dotsider.Analysis;
using Dotsider.Analysis.Models;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class TreemapLayoutTests(SampleAssemblyFixture samples)
{
    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void Layout_NoOverlappingRects()
    {
        var nodes = CreateTestNodes(8);
        var rects = TreemapLayout.Layout(nodes, 0, 0, 100, 100);
        // TODO(human): Implement overlap detection
        // Given a list of TreemapRect, return true if any two rects overlap.
        // Two axis-aligned rects overlap if their X and Y ranges both intersect.
        // Use a tolerance (e.g., 0.1) for floating-point comparisons.
        AssertNoOverlaps(rects);
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void Layout_EmptyInput_ReturnsEmpty()
    {
        var rects = TreemapLayout.Layout([], 0, 0, 100, 100);
        Assert.Empty(rects);
    }

    [Fact(Timeout = 5_000)]
    public void Layout_ZeroWidthOrHeight_ReturnsEmpty()
    {
        var nodes = CreateTestNodes(3);
        Assert.Empty(TreemapLayout.Layout(nodes, 0, 0, 0, 100));
        Assert.Empty(TreemapLayout.Layout(nodes, 0, 0, 100, 0));
    }

    [Fact(Timeout = 5_000)]
    public void Layout_RealSizeTree_ProducesValidRects()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        var rects = TreemapLayout.Layout(tree.Children, 0, 0, 800, 600);
        Assert.NotEmpty(rects);
        Assert.All(rects, r =>
        {
            Assert.True(r.Width >= 0);
            Assert.True(r.Height >= 0);
        });
    }

    [Fact(Timeout = 5_000)]
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

    [Fact(Timeout = 5_000)]
    public void Layout_RectsMatchInputNodes()
    {
        var nodes = CreateTestNodes(5);
        var rects = TreemapLayout.Layout(nodes, 0, 0, 100, 100);
        Assert.Equal(nodes.Count, rects.Count);
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

    private static void AssertNoOverlaps(IReadOnlyList<TreemapRect> rects)
    {
        // TODO(human): Implement overlap detection
        // Check that no two rectangles in the list overlap.
        // Two axis-aligned rectangles overlap when:
        //   rect1.X < rect2.X + rect2.Width AND
        //   rect1.X + rect1.Width > rect2.X AND
        //   rect1.Y < rect2.Y + rect2.Height AND
        //   rect1.Y + rect1.Height > rect2.Y
        // Use a tolerance of 0.1 for floating-point comparison.
        // Throw Assert.Fail($"Rects {i} and {j} overlap") on first violation.
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
