using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the size-budget document parser: string entries, object entries with names and
/// severity, and precise failures on malformed documents. Pure input-domain tests — the
/// parser takes JSON text, so JSON text is the real fixture.
/// </summary>
public class SizeBudgetFileTests
{
    /// <summary>Verifies string entries parse through the spec grammar.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_StringEntries_UseSpecGrammar()
    {
        var budgets = SizeBudgetFile.Parse(
            """{ "budgets": ["total:max=25mb", "ns=System.Text.Json:growth=10kb"] }""");

        Assert.Equal(2, budgets.Count);
        Assert.Equal(SizeBudgetScope.Total, budgets[0].Scope);
        Assert.Equal("System.Text.Json", budgets[1].Target);
    }

    /// <summary>Verifies the object form carries name, description, severity, and topN.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_ObjectEntry_CarriesNameSeverityTopN()
    {
        var budgets = SizeBudgetFile.Parse("""
            { "budgets": [ {
                "name": "JSON growth",
                "description": "Serializer bloat guard",
                "scope": "ns=System.Text.Json",
                "growth": "10kb",
                "severity": "warning",
                "topN": 5
            } ] }
            """);

        var budget = Assert.Single(budgets);
        Assert.Equal("JSON growth", budget.Name);
        Assert.Equal("Serializer bloat guard", budget.Description);
        Assert.Equal(SizeBudgetScope.Namespace, budget.Scope);
        Assert.Equal("System.Text.Json", budget.Target);
        Assert.Equal(10L * 1024, budget.MaxGrowthBytes);
        Assert.Equal(SizeBudgetSeverity.Warning, budget.Severity);
        Assert.Equal(5, budget.TopN);
    }

    /// <summary>Verifies string and object entries mix freely in one document.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_MixedEntries_ParseInOrder()
    {
        var budgets = SizeBudgetFile.Parse("""
            { "budgets": [
                "max=25mb",
                { "scope": "asm=MyApp", "max": "2mb" }
            ] }
            """);

        Assert.Equal(2, budgets.Count);
        Assert.Equal(SizeBudgetScope.Total, budgets[0].Scope);
        Assert.Equal(SizeBudgetScope.Assembly, budgets[1].Scope);
        Assert.Equal("MyApp", budgets[1].Target);
    }

    /// <summary>Verifies an object entry without a scope defaults to total.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_ObjectWithoutScope_DefaultsToTotal()
    {
        var budgets = SizeBudgetFile.Parse("""{ "budgets": [ { "max": "1mb" } ] }""");

        Assert.Equal(SizeBudgetScope.Total, Assert.Single(budgets).Scope);
    }

    /// <summary>Verifies every malformed document fails with a message locating the problem.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("not json", "not valid JSON")]
    [InlineData("""{ "nope": [] }""", "'budgets' array")]
    [InlineData("""{ "budgets": [42] }""", "budgets[0]")]
    [InlineData("""{ "budgets": [ {} ] }""", "'max' and/or 'growth'")]
    [InlineData("""{ "budgets": [ { "max": "1kb", "severity": "fatal" } ] }""", "severity")]
    [InlineData("""{ "budgets": [ { "max": "1kb", "topN": -1 } ] }""", "topN")]
    [InlineData("""{ "budgets": [ { "max": "1kb", "unknown": true } ] }""", "unknown property")]
    [InlineData("""{ "budgets": [ { "max": "zzz" } ] }""", "not a valid size")]
    public void Parse_InvalidDocument_ThrowsWithContext(string json, string messagePart)
    {
        var ex = Assert.Throws<FormatException>(() => SizeBudgetFile.Parse(json));
        Assert.Contains(messagePart, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
