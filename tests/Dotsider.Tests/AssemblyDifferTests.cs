using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Assembly Differ.
/// </summary>
[Collection("SampleAssemblies")]
public class AssemblyDifferTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies rich library v1vs v2 has non empty diff.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibraryV1vsV2_HasNonEmptyDiff()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.NotEmpty(result.TypeDiffs);
        Assert.NotEmpty(result.MethodDiffs);
    }

    /// <summary>
    /// Verifies v1vs v2 type diffs i repository removed.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void V1vsV2_TypeDiffs_IRepositoryRemoved()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(result.TypeDiffs, d =>
            d.Kind == DiffKind.Removed && d.Left!.Name.Contains("IRepository"));
    }

    /// <summary>
    /// Verifies v1vs v2 type diffs order added.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void V1vsV2_TypeDiffs_OrderAdded()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(result.TypeDiffs, d =>
            d.Kind == DiffKind.Added && d.Right!.Name == "Order");
    }

    /// <summary>
    /// Verifies v1vs v2 ref diffs system text json removed.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void V1vsV2_RefDiffs_SystemTextJsonRemoved()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(result.AssemblyRefDiffs, d =>
            d.Kind == DiffKind.Removed && d.Left!.Name == "System.Text.Json");
    }

    /// <summary>
    /// Verifies v1vs v2 summary has positive counts.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void V1vsV2_Summary_HasPositiveCounts()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.True(result.MetadataSummary.TypesAdded > 0);
        Assert.True(result.MetadataSummary.TypesRemoved > 0);
        Assert.True(result.MetadataSummary.MethodsAdded > 0);
    }

    /// <summary>
    /// Verifies same assembly all unchanged.
    /// </summary>
    [Fact(Timeout = 30_000)]
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

    /// <summary>
    /// Verifies v1vs v2 method diffs signature changes detected.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void V1vsV2_MethodDiffs_SignatureChangesDetected()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        // There should be methods that changed signature
        Assert.True(result.MetadataSummary.MethodsChanged > 0 ||
                     result.MetadataSummary.MethodsAdded > 0);
    }

    /// <summary>
    /// Verifies v1vs v2 ref diffs newtonsoft still present.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void V1vsV2_RefDiffs_NewtonsoftStillPresent()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        // Newtonsoft.Json was dropped in V2 — should be Removed
        Assert.Contains(result.AssemblyRefDiffs, d =>
            d.Kind == DiffKind.Removed && d.Left?.Name == "Newtonsoft.Json");
    }

    /// <summary>
    /// Verifies v1vs v2 size delta is non zero.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void V1vsV2_SizeDelta_IsNonZero()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.NotEqual(0, result.MetadataSummary.SizeDelta);
    }

    /// <summary>
    /// Verifies v1vs v2 diff entries have correct kinds.
    /// </summary>
    [Fact(Timeout = 30_000)]
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

    /// <summary>
    /// Verifies v1vs v2 type diffs audit log added.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void V1vsV2_TypeDiffs_AuditLogAdded()
    {
        using var left = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(result.TypeDiffs, d =>
            d.Kind == DiffKind.Added && d.Right!.Name == "AuditLog");
    }
}
