using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the size-budget spec grammar. These are pure input-domain tests — the grammar
/// takes strings, so strings are the real fixtures.
/// </summary>
[TestClass]
public class SizeBudgetParserTests
{
    /// <summary>Verifies a bare limit defaults to the total scope.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_BareMax_DefaultsToTotalScope()
    {
        var budget = SizeBudgetParser.Parse("max=25mb");

        Assert.AreEqual(SizeBudgetScope.Total, budget.Scope);
        Assert.IsNull(budget.Target);
        Assert.AreEqual(25L * 1024 * 1024, budget.MaxBytes);
        Assert.IsNull(budget.MaxGrowthBytes);
        Assert.IsNull(budget.MaxGrowthPercent);
        Assert.AreEqual(SizeBudgetSeverity.Error, budget.Severity);
    }

    /// <summary>Verifies percentage growth parses on the total scope.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_GrowthPercent_Parses()
    {
        var budget = SizeBudgetParser.Parse("growth=1.5%");

        Assert.AreEqual(SizeBudgetScope.Total, budget.Scope);
        Assert.AreEqual(1.5, budget.MaxGrowthPercent);
        Assert.IsNull(budget.MaxGrowthBytes);
    }

    /// <summary>Verifies an explicit total scope with multiple limits parses both.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_TotalScopeMultiLimit_ParsesBoth()
    {
        var budget = SizeBudgetParser.Parse("total:max=25mb,growth=50kb");

        Assert.AreEqual(SizeBudgetScope.Total, budget.Scope);
        Assert.AreEqual(25L * 1024 * 1024, budget.MaxBytes);
        Assert.AreEqual(50L * 1024, budget.MaxGrowthBytes);
    }

    /// <summary>Verifies a namespace scope keeps its dotted target intact.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_NamespaceScope_KeepsTarget()
    {
        var budget = SizeBudgetParser.Parse("ns=System.Text.Json:growth=10kb");

        Assert.AreEqual(SizeBudgetScope.Namespace, budget.Scope);
        Assert.AreEqual("System.Text.Json", budget.Target);
        Assert.AreEqual(10L * 1024, budget.MaxGrowthBytes);
    }

    /// <summary>Verifies an assembly scope parses with mixed limits.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_AssemblyScope_ParsesMixedLimits()
    {
        var budget = SizeBudgetParser.Parse("asm=MyApp:max=2mb,growth=5%");

        Assert.AreEqual(SizeBudgetScope.Assembly, budget.Scope);
        Assert.AreEqual("MyApp", budget.Target);
        Assert.AreEqual(2L * 1024 * 1024, budget.MaxBytes);
        Assert.AreEqual(5.0, budget.MaxGrowthPercent);
    }

    /// <summary>Verifies unit handling: binary units, explicit bytes, and bare numbers.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("max=4096", 4096L)]
    [DataRow("max=4096b", 4096L)]
    [DataRow("max=1kb", 1024L)]
    [DataRow("max=1KB", 1024L)]
    [DataRow("max=1.5kb", 1536L)]
    [DataRow("max=1gb", 1024L * 1024 * 1024)]
    public void Parse_SizeUnits_ResolveToBytes(string spec, long expected)
    {
        Assert.AreEqual(expected, SizeBudgetParser.Parse(spec).MaxBytes);
    }

    /// <summary>Verifies zero growth parses — the "no growth at all" gate.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_ZeroGrowth_Parses()
    {
        var budget = SizeBudgetParser.Parse("ns=NativeAotConsole.Telemetry:growth=0");

        Assert.AreEqual(0L, budget.MaxGrowthBytes);
    }

    /// <summary>Verifies scope and keyword casing are forgiven.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_CaseInsensitiveKeywords_Parse()
    {
        var budget = SizeBudgetParser.Parse("NS=Foo:GROWTH=10KB");

        Assert.AreEqual(SizeBudgetScope.Namespace, budget.Scope);
        Assert.AreEqual("Foo", budget.Target);
        Assert.AreEqual(10L * 1024, budget.MaxGrowthBytes);
    }

    /// <summary>Verifies every malformed spec fails with a message naming the offending part.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("", "empty")]
    [DataRow("   ", "empty")]
    [DataRow("total", "limit")]
    [DataRow("ns=Foo", "limit")]
    [DataRow("total:", "limit")]
    [DataRow("ns=:growth=1kb", "namespace")]
    [DataRow("asm=:max=1kb", "assembly")]
    [DataRow("weird=Foo:max=1kb", "unknown scope")]
    [DataRow("cap=25mb", "max=SIZE or growth=")]
    [DataRow("max=25zb", "not a valid size")]
    [DataRow("max=abc", "not a valid size")]
    [DataRow("max=-5", "not a valid size")]
    [DataRow("max=5%", "growth")]
    [DataRow("max=1kb,max=2kb", "duplicate")]
    [DataRow("growth=1%,growth=2%", "duplicate")]
    public void Parse_InvalidSpec_ThrowsWithContext(string spec, string messagePart)
    {
        var ex = Assert.ThrowsExactly<FormatException>(() => SizeBudgetParser.Parse(spec));
        Assert.Contains(messagePart, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies a parsed budget renders back into grammar form for display.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ToString_RendersSpecForm()
    {
        Assert.AreEqual("ns=Foo:growth=10240",
            SizeBudgetParser.Parse("ns=Foo:growth=10kb").ToString());
        Assert.AreEqual("total:max=1024", SizeBudgetParser.Parse("max=1kb").ToString());
    }
}
