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

    // --- JIT detail parsing tests ---

    [Theory]
    [InlineData("System.String.Concat", "System.String", "Concat")]
    [InlineData("MyApp.Services.UserService.GetUser", "MyApp.Services.UserService", "GetUser")]
    [InlineData("GlobalType.Run", "GlobalType", "Run")]
    public void TryParseJitDetail_ValidFormat_ReturnsTrueWithComponents(
        string detail, string expectedType, string expectedMethod)
    {
        Assert.True(DynamicAnalysisView.TryParseJitDetail(detail, out var declaringType, out var methodName));
        Assert.Equal(expectedType, declaringType);
        Assert.Equal(expectedMethod, methodName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NoDotHere")]
    [InlineData(".LeadingDot")]
    public void TryParseJitDetail_InvalidFormat_ReturnsFalse(string detail)
    {
        Assert.False(DynamicAnalysisView.TryParseJitDetail(detail, out _, out _));
    }

    [Fact]
    public void TryParseJitDetail_TrailingDot_ReturnsFalse()
    {
        Assert.False(DynamicAnalysisView.TryParseJitDetail("System.String.", out _, out _));
    }
}
