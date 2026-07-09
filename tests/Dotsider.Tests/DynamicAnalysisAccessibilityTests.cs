using Dotsider.Core.Analysis.Models;
using Dotsider.Views;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Dynamic Analysis Accessibility.
/// </summary>
[TestClass]
public class DynamicAnalysisAccessibilityTests
{
    /// <summary>
    /// Verifies category colors all categories have entries.
    /// </summary>
    [TestMethod]
    public void CategoryColors_AllCategoriesHaveEntries()
    {
        foreach (var category in Enum.GetValues<TraceEventCategory>())
            Assert.IsTrue(DynamicAnalysisView.CategoryColors.ContainsKey(category),
                $"Missing color for {category}");
    }

    /// <summary>
    /// Verifies category colors no duplicate colors.
    /// </summary>
    [TestMethod]
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
            Assert.IsFalse(seen.TryGetValue(key, out var existing),
                $"{category} shares color ({color.R},{color.G},{color.B}) with {existing}");
            seen[key] = category;
        }
    }

    /// <summary>
    /// Verifies socket and http have different colors.
    /// </summary>
    [TestMethod]
    public void SocketAndHttp_HaveDifferentColors()
    {
        var httpColor = DynamicAnalysisView.CategoryColors[TraceEventCategory.Http];
        var socketColor = DynamicAnalysisView.CategoryColors[TraceEventCategory.Socket];
        Assert.IsFalse(
            httpColor.R == socketColor.R && httpColor.G == socketColor.G && httpColor.B == socketColor.B,
            "Http and Socket must have distinct colors for accessibility");
    }

    // --- JIT detail parsing tests ---

    /// <summary>
    /// Verifies try parse jit detail valid format returns true with components.
    /// </summary>
    [TestMethod]
    [DataRow("System.String.Concat", "System.String", "Concat")]
    [DataRow("MyApp.Services.UserService.GetUser", "MyApp.Services.UserService", "GetUser")]
    [DataRow("GlobalType.Run", "GlobalType", "Run")]
    public void TryParseJitDetail_ValidFormat_ReturnsTrueWithComponents(
        string detail, string expectedType, string expectedMethod)
    {
        Assert.IsTrue(DynamicAnalysisView.TryParseJitDetail(detail, out var declaringType, out var methodName));
        Assert.AreEqual(expectedType, declaringType);
        Assert.AreEqual(expectedMethod, methodName);
    }

    /// <summary>
    /// Verifies try parse jit detail invalid format returns false.
    /// </summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("NoDotHere")]
    [DataRow(".LeadingDot")]
    public void TryParseJitDetail_InvalidFormat_ReturnsFalse(string detail)
    {
        Assert.IsFalse(DynamicAnalysisView.TryParseJitDetail(detail, out _, out _));
    }

    /// <summary>
    /// Verifies try parse jit detail trailing dot returns false.
    /// </summary>
    [TestMethod]
    public void TryParseJitDetail_TrailingDot_ReturnsFalse()
    {
        Assert.IsFalse(DynamicAnalysisView.TryParseJitDetail("System.String.", out _, out _));
    }
}
