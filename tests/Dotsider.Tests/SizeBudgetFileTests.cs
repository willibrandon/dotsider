using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the size-budget document parser: string entries, object entries with names and
/// severity, and precise failures on malformed documents. Pure input-domain tests — the
/// parser takes JSON text, so JSON text is the real fixture.
/// </summary>
[TestClass]
public class SizeBudgetFileTests
{
    /// <summary>Verifies string entries parse through the spec grammar.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_StringEntries_UseSpecGrammar()
    {
        var budgets = SizeBudgetFile.Parse(
            """{ "budgets": ["total:max=25mb", "ns=System.Text.Json:growth=10kb"] }""");

        Assert.HasCount(2, budgets);
        Assert.AreEqual(SizeBudgetScope.Total, budgets[0].Scope);
        Assert.AreEqual("System.Text.Json", budgets[1].Target);
    }

    /// <summary>Verifies the object form carries name, description, severity, and topN.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        var budget = Assert.ContainsSingle(budgets);
        Assert.AreEqual("JSON growth", budget.Name);
        Assert.AreEqual("Serializer bloat guard", budget.Description);
        Assert.AreEqual(SizeBudgetScope.Namespace, budget.Scope);
        Assert.AreEqual("System.Text.Json", budget.Target);
        Assert.AreEqual(10L * 1024, budget.MaxGrowthBytes);
        Assert.AreEqual(SizeBudgetSeverity.Warning, budget.Severity);
        Assert.AreEqual(5, budget.TopN);
    }

    /// <summary>Verifies string and object entries mix freely in one document.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_MixedEntries_ParseInOrder()
    {
        var budgets = SizeBudgetFile.Parse("""
            { "budgets": [
                "max=25mb",
                { "scope": "asm=MyApp", "max": "2mb" }
            ] }
            """);

        Assert.HasCount(2, budgets);
        Assert.AreEqual(SizeBudgetScope.Total, budgets[0].Scope);
        Assert.AreEqual(SizeBudgetScope.Assembly, budgets[1].Scope);
        Assert.AreEqual("MyApp", budgets[1].Target);
    }

    /// <summary>Verifies an object entry without a scope defaults to total.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_ObjectWithoutScope_DefaultsToTotal()
    {
        var budgets = SizeBudgetFile.Parse("""{ "budgets": [ { "max": "1mb" } ] }""");

        Assert.AreEqual(SizeBudgetScope.Total, Assert.ContainsSingle(budgets).Scope);
    }

    /// <summary>Verifies every malformed document fails with a message locating the problem.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("not json", "not valid JSON")]
    [DataRow("""{ "nope": [] }""", "'budgets' array")]
    [DataRow("""{ "budgets": [42] }""", "budgets[0]")]
    [DataRow("""{ "budgets": [ {} ] }""", "'max' and/or 'growth'")]
    [DataRow("""{ "budgets": [ { "max": "1kb", "severity": "fatal" } ] }""", "severity")]
    [DataRow("""{ "budgets": [ { "max": "1kb", "topN": -1 } ] }""", "topN")]
    [DataRow("""{ "budgets": [ { "max": "1kb", "unknown": true } ] }""", "unknown property")]
    [DataRow("""{ "budgets": [ { "max": "zzz" } ] }""", "not a valid size")]
    public void Parse_InvalidDocument_ThrowsWithContext(string json, string messagePart)
    {
        var ex = Assert.ThrowsExactly<FormatException>(() => SizeBudgetFile.Parse(json));
        Assert.Contains(messagePart, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
