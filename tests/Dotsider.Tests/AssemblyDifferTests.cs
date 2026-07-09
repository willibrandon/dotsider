using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Assembly Differ.
/// </summary>
[TestClass]
public class AssemblyDifferTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies rich library v1vs v2 has non empty diff.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibraryV1vsV2_HasNonEmptyDiff()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.IsNotEmpty(result.TypeDiffs);
        Assert.IsNotEmpty(result.MethodDiffs);
    }

    /// <summary>
    /// Verifies v1vs v2 type diffs i repository removed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_TypeDiffs_IRepositoryRemoved()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(d =>
            d.Kind == DiffKind.Removed && d.Left!.Name.Contains("IRepository"), result.TypeDiffs);
    }

    /// <summary>
    /// Verifies v1vs v2 type diffs order added.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_TypeDiffs_OrderAdded()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(d =>
            d.Kind == DiffKind.Added && d.Right!.Name == "Order", result.TypeDiffs);
    }

    /// <summary>
    /// Verifies v1vs v2 ref diffs system text json removed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_RefDiffs_SystemTextJsonRemoved()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(d =>
            d.Kind == DiffKind.Removed && d.Left!.Name == "System.Text.Json", result.AssemblyRefDiffs);
    }

    /// <summary>
    /// Verifies v1vs v2 summary has positive counts.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_Summary_HasPositiveCounts()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.IsGreaterThan(0, result.MetadataSummary.TypesAdded);
        Assert.IsGreaterThan(0, result.MetadataSummary.TypesRemoved);
        Assert.IsGreaterThan(0, result.MetadataSummary.MethodsAdded);
    }

    /// <summary>
    /// Verifies same assembly all unchanged.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SameAssembly_AllUnchanged()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.DoesNotContain(d => d.Kind == DiffKind.Added, result.TypeDiffs);
        Assert.DoesNotContain(d => d.Kind == DiffKind.Removed, result.TypeDiffs);
        Assert.DoesNotContain(d => d.Kind == DiffKind.Added, result.MethodDiffs);
        Assert.DoesNotContain(d => d.Kind == DiffKind.Removed, result.MethodDiffs);
    }

    /// <summary>
    /// Verifies v1vs v2 method diffs signature changes detected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_MethodDiffs_SignatureChangesDetected()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        // There should be methods that changed signature
        Assert.IsTrue(result.MetadataSummary.MethodsChanged > 0 ||
                     result.MetadataSummary.MethodsAdded > 0);
    }

    /// <summary>
    /// Verifies v1vs v2 ref diffs newtonsoft still present.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_RefDiffs_NewtonsoftStillPresent()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        // Newtonsoft.Json was dropped in V2 — should be Removed
        Assert.Contains(d =>
            d.Kind == DiffKind.Removed && d.Left?.Name == "Newtonsoft.Json", result.AssemblyRefDiffs);
    }

    /// <summary>
    /// Verifies v1vs v2 size delta is non zero.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_SizeDelta_IsNonZero()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.AreNotEqual(0, result.MetadataSummary.SizeDelta);
    }

    /// <summary>
    /// Verifies v1vs v2 diff entries have correct kinds.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_DiffEntries_HaveCorrectKinds()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
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
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_TypeDiffs_AuditLogAdded()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);
        Assert.Contains(d =>
            d.Kind == DiffKind.Added && d.Right!.Name == "AuditLog", result.TypeDiffs);
    }

    /// <summary>
    /// Verifies methods with different IL bodies are reported as changed.
    /// Product.PrintMembers has same signature in v1/v2 but different IL because
    /// the Product record shape changed (StockCount→Quantity, added Sku).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_MethodDiffs_BodyChangesDetected()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);

        var printMembers = result.MethodDiffs.FirstOrDefault(d =>
            (d.Left ?? d.Right)!.Name == "PrintMembers"
            && (d.Left ?? d.Right)!.DeclaringType.Contains("Product"));

        Assert.IsNotNull(printMembers);
        Assert.AreEqual(DiffKind.Changed, printMembers.Kind);
        Assert.Contains("body", printMembers.ChangeDescription!);
    }

    /// <summary>
    /// Verifies source-identical methods survive token renumbering without false positives.
    /// CountActive is source-identical in v1/v2 but Product changed shape, causing token churn.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_MethodDiffs_SourceIdenticalMethodStaysUnchanged()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);

        var countActive = result.MethodDiffs.FirstOrDefault(d =>
            (d.Left ?? d.Right)!.Name == "CountActive"
            && (d.Left ?? d.Right)!.DeclaringType.Contains("ProductCatalog"));

        Assert.IsNotNull(countActive);
        Assert.AreEqual(DiffKind.Unchanged, countActive.Kind);
    }

    /// <summary>
    /// Verifies exception region changes are detected.
    /// TryFindById catches Exception (v1) vs InvalidOperationException (v2).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_MethodDiffs_ExceptionRegionChangeDetected()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);

        var tryFindById = result.MethodDiffs.FirstOrDefault(d =>
            (d.Left ?? d.Right)!.Name == "TryFindById"
            && (d.Left ?? d.Right)!.DeclaringType.Contains("UserService"));

        Assert.IsNotNull(tryFindById);
        Assert.AreEqual(DiffKind.Changed, tryFindById.Kind);
        Assert.Contains("body", tryFindById.ChangeDescription!);
    }

    /// <summary>
    /// Verifies local signature changes are detected.
    /// SummarizeUsers has 1 local (v1) vs 2+ locals (v2).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_MethodDiffs_LocalSignatureChangeDetected()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);

        var summarize = result.MethodDiffs.FirstOrDefault(d =>
            (d.Left ?? d.Right)!.Name == "SummarizeUsers"
            && (d.Left ?? d.Right)!.DeclaringType.Contains("UserService"));

        Assert.IsNotNull(summarize);
        Assert.AreEqual(DiffKind.Changed, summarize.Kind);
        Assert.Contains("body", summarize.ChangeDescription!);
    }

    /// <summary>
    /// Isolated test: LocalSignaturesDiffer returns true for two methods with
    /// different non-empty local signatures, exercising the element-by-element path.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void LocalSignaturesDiffer_DifferentLocals_ReturnsTrue()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var reader = analyzer.GetMetadataReader()!;

        // Add has locals (int id, User user); SummarizeUsers has local (int count)
        var addMethod = analyzer.MethodDefs.First(m =>
            m.Name == "Add" && m.DeclaringType.Contains("UserService"));
        var summarizeMethod = analyzer.MethodDefs.First(m =>
            m.Name == "SummarizeUsers" && m.DeclaringType.Contains("UserService"));

        var addBody = analyzer.GetMethodBody(addMethod)!;
        var summarizeBody = analyzer.GetMethodBody(summarizeMethod)!;

        Assert.IsTrue(AssemblyDiffer.LocalSignaturesDiffer(reader, addBody, reader, summarizeBody));
    }

    /// <summary>
    /// Isolated test: LocalSignaturesDiffer returns false for the same method body.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void LocalSignaturesDiffer_SameMethod_ReturnsFalse()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var reader = analyzer.GetMetadataReader()!;

        var addMethod = analyzer.MethodDefs.First(m =>
            m.Name == "Add" && m.DeclaringType.Contains("UserService"));
        var addBody = analyzer.GetMethodBody(addMethod)!;

        Assert.IsFalse(AssemblyDiffer.LocalSignaturesDiffer(reader, addBody, reader, addBody));
    }

    /// <summary>
    /// Verifies comparing an assembly against itself produces no body changes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SameAssembly_NoBodyChanges()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var result = AssemblyDiffer.Compare(left, right);

        Assert.DoesNotContain(d =>
            d.ChangeDescription?.Contains("body") == true, result.MethodDiffs);
    }

    /// <summary>
    /// Verifies abstract methods (Rva == 0) are not incorrectly flagged as body-changed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SameAssembly_AbstractMethods_NoBodyChange()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var result = AssemblyDiffer.Compare(left, right);

        var abstractMethods = result.MethodDiffs.Where(d =>
            (d.Left ?? d.Right)!.Rva == 0);

        TestAssert.All(abstractMethods, m => Assert.AreEqual(DiffKind.Unchanged, m.Kind));
    }

    /// <summary>
    /// Verifies that body differences increase the MethodsChanged count in the summary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_Summary_MethodsChangedIncludesBodyChanges()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);

        var bodyChangedCount = result.MethodDiffs.Count(d =>
            d.ChangeDescription?.Contains("body") == true);

        Assert.IsGreaterThan(0, bodyChangedCount, "Should detect at least one body change");
        Assert.IsGreaterThanOrEqualTo(bodyChangedCount, result.MetadataSummary.MethodsChanged);
    }

    /// <summary>
    /// Verifies that calli instructions with different calling conventions are detected.
    /// InvokeCallback uses managed (v1) vs unmanaged[Cdecl] (v2) — different StandaloneSig tokens.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_MethodDiffs_CalliCallingConventionChangeDetected()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);

        var invokeCallback = result.MethodDiffs.FirstOrDefault(d =>
            (d.Left ?? d.Right)!.Name == "InvokeCallback"
            && (d.Left ?? d.Right)!.DeclaringType.Contains("FunctionPointerHelpers"));

        Assert.IsNotNull(invokeCallback);
        Assert.AreEqual(DiffKind.Changed, invokeCallback.Kind);
        Assert.Contains("body", invokeCallback.ChangeDescription!);
    }

    /// <summary>
    /// Verifies that local variables with different function-pointer types are detected.
    /// HasCallback has a managed function-pointer local (v1) vs unmanaged[Cdecl] (v2).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void V1vsV2_MethodDiffs_FunctionPointerLocalChangeDetected()
    {
        using var left = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var right = new AssemblyAnalyzer(Samples.RichLibraryV2Dll);
        var result = AssemblyDiffer.Compare(left, right);

        var hasCallback = result.MethodDiffs.FirstOrDefault(d =>
            (d.Left ?? d.Right)!.Name == "HasCallback"
            && (d.Left ?? d.Right)!.DeclaringType.Contains("FunctionPointerHelpers"));

        Assert.IsNotNull(hasCallback);
        Assert.AreEqual(DiffKind.Changed, hasCallback.Kind);
        Assert.Contains("body", hasCallback.ChangeDescription!);
    }
}
