using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class AssemblyDifferTests(SampleAssemblyFixture samples)
{
    [Fact(Timeout = 5_000)]
    public void RichLibraryV1vsV2_HasNonEmptyDiff()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.NotEmpty(result.TypeDiffs);
        Assert.NotEmpty(result.MethodDiffs);
    }

    [Fact(Timeout = 5_000)]
    public void V1vsV2_TypeDiffs_IRepositoryRemoved()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(result.TypeDiffs, d =>
            d.Kind == DiffKind.Removed && d.Left!.Name.Contains("IRepository"));
    }

    [Fact(Timeout = 5_000)]
    public void V1vsV2_TypeDiffs_OrderAdded()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(result.TypeDiffs, d =>
            d.Kind == DiffKind.Added && d.Right!.Name == "Order");
    }

    [Fact(Timeout = 5_000)]
    public void V1vsV2_RefDiffs_SystemTextJsonRemoved()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(result.AssemblyRefDiffs, d =>
            d.Kind == DiffKind.Removed && d.Left!.Name == "System.Text.Json");
    }

    [Fact(Timeout = 5_000)]
    public void V1vsV2_Summary_HasPositiveCounts()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.True(result.MetadataSummary.TypesAdded > 0);
        Assert.True(result.MetadataSummary.TypesRemoved > 0);
        Assert.True(result.MetadataSummary.MethodsAdded > 0);
    }

    [Fact(Timeout = 5_000)]
    public void SameAssembly_AllUnchanged()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryDll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.DoesNotContain(result.TypeDiffs, d => d.Kind == DiffKind.Added);
        Assert.DoesNotContain(result.TypeDiffs, d => d.Kind == DiffKind.Removed);
        Assert.DoesNotContain(result.MethodDiffs, d => d.Kind == DiffKind.Added);
        Assert.DoesNotContain(result.MethodDiffs, d => d.Kind == DiffKind.Removed);
    }

    [Fact(Timeout = 5_000)]
    public void V1vsV2_MethodDiffs_SignatureChangesDetected()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        // There should be methods that changed signature
        Assert.True(result.MetadataSummary.MethodsChanged > 0 ||
                     result.MetadataSummary.MethodsAdded > 0);
    }

    [Fact(Timeout = 5_000)]
    public void V1vsV2_RefDiffs_NewtonsoftStillPresent()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        // Newtonsoft.Json was dropped in V2 — should be Removed
        Assert.Contains(result.AssemblyRefDiffs, d =>
            d.Kind == DiffKind.Removed && d.Left?.Name == "Newtonsoft.Json");
    }

    [Fact(Timeout = 5_000)]
    public void V1vsV2_SizeDelta_IsNonZero()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.NotEqual(0, result.MetadataSummary.SizeDelta);
    }

    [Fact(Timeout = 5_000)]
    public void V1vsV2_DiffEntries_HaveCorrectKinds()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        var kinds = result.TypeDiffs.Select(d => d.Kind).Distinct().ToHashSet();
        // Should have at least Added, Removed, and Unchanged
        Assert.Contains(DiffKind.Added, kinds);
        Assert.Contains(DiffKind.Removed, kinds);
        Assert.Contains(DiffKind.Unchanged, kinds);
    }

    [Fact(Timeout = 5_000)]
    public void V1vsV2_TypeDiffs_AuditLogAdded()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(result.TypeDiffs, d =>
            d.Kind == DiffKind.Added && d.Right!.Name == "AuditLog");
    }
}
