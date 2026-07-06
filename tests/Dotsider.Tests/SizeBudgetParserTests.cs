using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the size-budget spec grammar. These are pure input-domain tests — the grammar
/// takes strings, so strings are the real fixtures.
/// </summary>
public class SizeBudgetParserTests
{
    /// <summary>Verifies a bare limit defaults to the total scope.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_BareMax_DefaultsToTotalScope()
    {
        var budget = SizeBudgetParser.Parse("max=25mb");

        Assert.Equal(SizeBudgetScope.Total, budget.Scope);
        Assert.Null(budget.Target);
        Assert.Equal(25L * 1024 * 1024, budget.MaxBytes);
        Assert.Null(budget.MaxGrowthBytes);
        Assert.Null(budget.MaxGrowthPercent);
        Assert.Equal(SizeBudgetSeverity.Error, budget.Severity);
    }

    /// <summary>Verifies percentage growth parses on the total scope.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_GrowthPercent_Parses()
    {
        var budget = SizeBudgetParser.Parse("growth=1.5%");

        Assert.Equal(SizeBudgetScope.Total, budget.Scope);
        Assert.Equal(1.5, budget.MaxGrowthPercent);
        Assert.Null(budget.MaxGrowthBytes);
    }

    /// <summary>Verifies an explicit total scope with multiple limits parses both.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_TotalScopeMultiLimit_ParsesBoth()
    {
        var budget = SizeBudgetParser.Parse("total:max=25mb,growth=50kb");

        Assert.Equal(SizeBudgetScope.Total, budget.Scope);
        Assert.Equal(25L * 1024 * 1024, budget.MaxBytes);
        Assert.Equal(50L * 1024, budget.MaxGrowthBytes);
    }

    /// <summary>Verifies a namespace scope keeps its dotted target intact.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_NamespaceScope_KeepsTarget()
    {
        var budget = SizeBudgetParser.Parse("ns=System.Text.Json:growth=10kb");

        Assert.Equal(SizeBudgetScope.Namespace, budget.Scope);
        Assert.Equal("System.Text.Json", budget.Target);
        Assert.Equal(10L * 1024, budget.MaxGrowthBytes);
    }

    /// <summary>Verifies an assembly scope parses with mixed limits.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_AssemblyScope_ParsesMixedLimits()
    {
        var budget = SizeBudgetParser.Parse("asm=MyApp:max=2mb,growth=5%");

        Assert.Equal(SizeBudgetScope.Assembly, budget.Scope);
        Assert.Equal("MyApp", budget.Target);
        Assert.Equal(2L * 1024 * 1024, budget.MaxBytes);
        Assert.Equal(5.0, budget.MaxGrowthPercent);
    }

    /// <summary>Verifies unit handling: binary units, explicit bytes, and bare numbers.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("max=4096", 4096L)]
    [InlineData("max=4096b", 4096L)]
    [InlineData("max=1kb", 1024L)]
    [InlineData("max=1KB", 1024L)]
    [InlineData("max=1.5kb", 1536L)]
    [InlineData("max=1gb", 1024L * 1024 * 1024)]
    public void Parse_SizeUnits_ResolveToBytes(string spec, long expected)
    {
        Assert.Equal(expected, SizeBudgetParser.Parse(spec).MaxBytes);
    }

    /// <summary>Verifies zero growth parses — the "no growth at all" gate.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_ZeroGrowth_Parses()
    {
        var budget = SizeBudgetParser.Parse("ns=NativeAotConsole.Telemetry:growth=0");

        Assert.Equal(0L, budget.MaxGrowthBytes);
    }

    /// <summary>Verifies scope and keyword casing are forgiven.</summary>
    [Fact(Timeout = 30_000)]
    public void Parse_CaseInsensitiveKeywords_Parse()
    {
        var budget = SizeBudgetParser.Parse("NS=Foo:GROWTH=10KB");

        Assert.Equal(SizeBudgetScope.Namespace, budget.Scope);
        Assert.Equal("Foo", budget.Target);
        Assert.Equal(10L * 1024, budget.MaxGrowthBytes);
    }

    /// <summary>Verifies every malformed spec fails with a message naming the offending part.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("total", "limit")]
    [InlineData("ns=Foo", "limit")]
    [InlineData("total:", "limit")]
    [InlineData("ns=:growth=1kb", "namespace")]
    [InlineData("asm=:max=1kb", "assembly")]
    [InlineData("weird=Foo:max=1kb", "unknown scope")]
    [InlineData("cap=25mb", "max=SIZE or growth=")]
    [InlineData("max=25zb", "not a valid size")]
    [InlineData("max=abc", "not a valid size")]
    [InlineData("max=-5", "not a valid size")]
    [InlineData("max=5%", "growth")]
    [InlineData("max=1kb,max=2kb", "duplicate")]
    [InlineData("growth=1%,growth=2%", "duplicate")]
    public void Parse_InvalidSpec_ThrowsWithContext(string spec, string messagePart)
    {
        var ex = Assert.Throws<FormatException>(() => SizeBudgetParser.Parse(spec));
        Assert.Contains(messagePart, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies a parsed budget renders back into grammar form for display.</summary>
    [Fact(Timeout = 30_000)]
    public void ToString_RendersSpecForm()
    {
        Assert.Equal("ns=Foo:growth=10240",
            SizeBudgetParser.Parse("ns=Foo:growth=10kb").ToString());
        Assert.Equal("total:max=1024", SizeBudgetParser.Parse("max=1kb").ToString());
    }
}
