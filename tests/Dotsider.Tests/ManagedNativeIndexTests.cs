using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Reflection;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the managed↔native correlation index: join rules against hand-written
/// ILC-form symbol names, mstat evidence, shared-pool accounting, and lookups.
/// </summary>
[TestClass]
public class ManagedNativeIndexTests
{
    private static MethodDefInfo Method(string declaringType, string name, int token) =>
        new(token, declaringType, name, "()", MethodAttributes.Public, MethodImplAttributes.IL, 0x2000);

    private static NativeSymbol Symbol(string name, ulong va, long size) =>
        new(name, null, va, null, null, ".text", size, NativeSymbolKind.Function, null, null, false, []);

    private static MstatMethod MstatRow(string declaringType, string name, int size, string assembly = "App") =>
        new(name, declaringType, "", assembly, size, 0, 0, null);

    private static MstatData Mstat(params MstatMethod[] methods) =>
        new(2, 2, [], methods, [], [], [], [], [], []);

    private static ManagedNativeIndex Build(
        IReadOnlyList<ManagedMethodSource> sources,
        IReadOnlyList<NativeSymbol> symbols,
        MstatData? mstat = null) =>
        ManagedNativeIndex.Build(sources, symbols, mstat);

    /// <summary>Verifies a unique method joins its symbol exactly and owns its size.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_UniqueMethodWithSymbol_CorrelatedExact()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Run", 0x06000001)])],
            [Symbol("App_Greeter__Run", 0x1000, 64)]);

        var correlation = index.Find("App", 0x06000001);
        Assert.IsNotNull(correlation);
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, correlation!.Status);
        Assert.ContainsSingle(correlation.NativeSymbols);
        Assert.AreEqual(64, correlation.NativeSize);
        Assert.AreEqual(0, correlation.SharedCandidateSize);
        Assert.AreEqual(1, index.ExactCount);
        Assert.AreEqual(64, index.TotalCorrelatedSize);
    }

    /// <summary>Verifies overloads share their evidence pool: both ambiguous, own nothing, counted once.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_OverloadsWithSuffixedSymbols_SharedAmbiguousPool()
    {
        var index = Build(
            [new ManagedMethodSource("App",
            [
                Method("Greeter", "Greet", 0x06000002),
                Method("Greeter", "Greet", 0x06000003),
            ])],
            [
                Symbol("App_Greeter__Greet_0", 0x1000, 100),
                Symbol("App_Greeter__Greet_1", 0x2000, 60),
            ]);

        var first = index.Find("App", 0x06000002)!;
        var second = index.Find("App", 0x06000003)!;
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedAmbiguous, first.Status);
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedAmbiguous, second.Status);
        Assert.HasCount(2, first.NativeSymbols);
        Assert.AreEqual(0, first.NativeSize);
        Assert.AreEqual(160, first.SharedCandidateSize);
        Assert.AreEqual(160, second.SharedCandidateSize);
        Assert.AreEqual(160, index.TotalCorrelatedSize); // pool counted once, never per candidate
        Assert.AreEqual(2, index.AmbiguousCount);
    }

    /// <summary>Verifies generic instantiations accumulate on one method as an exact join.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_GenericInstantiations_MultiSymbolExact()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Describe", 0x06000004)])],
            [
                Symbol("App_Greeter__Describe<Int32>", 0x1000, 40),
                Symbol("App_Greeter__Describe<System___Canon>", 0x2000, 52),
            ]);

        var correlation = index.Find("App", 0x06000004)!;
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, correlation.Status);
        Assert.HasCount(2, correlation.NativeSymbols);
        Assert.AreEqual(92, correlation.NativeSize);
    }

    /// <summary>Verifies constructor and accessor names join through the shared sanitize rules.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_CtorAndAccessor_Join()
    {
        var index = Build(
            [new ManagedMethodSource("App",
            [
                Method("Greeter", ".ctor", 0x06000005),
                Method("Greeter", "get_Name", 0x06000006),
                Method("Greeter", ".cctor", 0x06000007),
            ])],
            [
                Symbol("App_Greeter___ctor", 0x1000, 24),
                Symbol("App_Greeter__get_Name", 0x2000, 8),
                Symbol("App_Greeter___cctor", 0x3000, 16),
            ]);

        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, index.Find("App", 0x06000005)!.Status);
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, index.Find("App", 0x06000006)!.Status);
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, index.Find("App", 0x06000007)!.Status);
    }

    /// <summary>Verifies a method with no evidence at all is reported as not in the native image.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_NoEvidence_NotInNativeImage()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "NeverCalled", 0x06000008)])],
            []);

        var correlation = index.Find("App", 0x06000008)!;
        Assert.AreEqual(MethodCorrelationStatus.NotInNativeImage, correlation.Status);
        Assert.AreEqual(0, correlation.NativeSize);
        Assert.AreEqual(1, index.NotInImageCount);
        Assert.AreEqual(0, index.TotalCorrelatedSize);
    }

    /// <summary>Verifies mstat-only evidence yields the size-only status with an owned size.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_MstatOnlyEvidence_CorrelatedByMstatOnly()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "SizeOnly", 0x06000009)])],
            [],
            Mstat(MstatRow("Greeter", "SizeOnly", 123)));

        var correlation = index.Find("App", 0x06000009)!;
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedByMstatOnly, correlation.Status);
        Assert.IsEmpty(correlation.NativeSymbols);
        Assert.AreEqual(123, correlation.NativeSize);
        Assert.AreEqual(1, index.MstatOnlyCount);
        Assert.AreEqual(123, index.TotalCorrelatedSize);
    }

    /// <summary>Verifies shared mstat evidence among overloads is never double-counted.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_SharedMstatEvidence_CountedOnce()
    {
        var index = Build(
            [new ManagedMethodSource("App",
            [
                Method("Greeter", "Greet", 0x0600000A),
                Method("Greeter", "Greet", 0x0600000B),
            ])],
            [],
            Mstat(MstatRow("Greeter", "Greet", 100), MstatRow("Greeter", "Greet", 100)));

        var first = index.Find("App", 0x0600000A)!;
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedByMstatOnly, first.Status);
        Assert.AreEqual(0, first.NativeSize);
        Assert.AreEqual(200, first.SharedCandidateSize);
        Assert.AreEqual(200, index.TotalCorrelatedSize);
    }

    /// <summary>Verifies mstat sizes are preferred over symbol sizes for the same method, never summed.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_MstatAndSymbolEvidence_MstatSizePreferred()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Run", 0x0600000C)])],
            [Symbol("App_Greeter__Run", 0x1000, 50)],
            Mstat(MstatRow("Greeter", "Run", 80)));

        var correlation = index.Find("App", 0x0600000C)!;
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, correlation.Status);
        Assert.AreEqual(80, correlation.NativeSize);
        Assert.AreEqual(80, index.TotalCorrelatedSize);
    }

    /// <summary>Verifies same-token methods in different assemblies stay distinct.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_MultiAssemblySources_TokenCollisionsStayDistinct()
    {
        const int token = 0x06000001;
        var index = Build(
            [
                new ManagedMethodSource("App", [Method("Greeter", "Run", token)]),
                new ManagedMethodSource("Lib", [Method("Greeter", "Run", token)]),
            ],
            [
                Symbol("App_Greeter__Run", 0x1000, 10),
                Symbol("Lib_Greeter__Run", 0x2000, 20),
            ]);

        var app = index.Find("App", token)!;
        var lib = index.Find("Lib", token)!;
        Assert.AreEqual(10, app.NativeSize);
        Assert.AreEqual(20, lib.NativeSize);
        Assert.AreEqual(0x1000UL, app.NativeSymbols[0].VirtualAddress);
        Assert.AreEqual(0x2000UL, lib.NativeSymbols[0].VirtualAddress);
    }

    /// <summary>Verifies mstat rows only join sources whose assembly name matches.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_MstatAssemblyFilter_ForeignRowsIgnored()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Run", 0x0600000D)])],
            [],
            Mstat(MstatRow("Greeter", "Run", 999, assembly: "Other")));

        Assert.AreEqual(MethodCorrelationStatus.NotInNativeImage, index.Find("App", 0x0600000D)!.Status);
    }

    /// <summary>Verifies mstat names lose only a balanced trailing instantiation group; &lt;Main&gt;$ survives.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void StripTrailingInstantiation_TrailingGroupOnly()
    {
        Assert.AreEqual("Describe", ManagedNativeIndex.StripTrailingInstantiation("Describe<Int32>"));
        Assert.AreEqual("List`1", ManagedNativeIndex.StripTrailingInstantiation("List`1<Int32>"));
        Assert.AreEqual("<Main>$", ManagedNativeIndex.StripTrailingInstantiation("<Main>$"));
        Assert.AreEqual("Plain", ManagedNativeIndex.StripTrailingInstantiation("Plain"));
        Assert.AreEqual("Outer<T>.Inner", ManagedNativeIndex.StripTrailingInstantiation("Outer<T>.Inner"));
    }

    /// <summary>Verifies an instantiated mstat row matches its definition.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_MstatInstantiatedRow_JoinsDefinition()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Describe", 0x0600000E)])],
            [],
            Mstat(MstatRow("Greeter", "Describe<Int32>", 42)));

        var correlation = index.Find("App", 0x0600000E)!;
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedByMstatOnly, correlation.Status);
        Assert.AreEqual(42, correlation.NativeSize);
    }

    /// <summary>Verifies reverse lookup by address, including the shared-pool first-candidate convention.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FindByAddress_OwnedAndShared_Resolve()
    {
        var index = Build(
            [new ManagedMethodSource("App",
            [
                Method("Greeter", "Run", 0x06000001),
                Method("Greeter", "Greet", 0x06000002),
                Method("Greeter", "Greet", 0x06000003),
            ])],
            [
                Symbol("App_Greeter__Run", 0x1000, 10),
                Symbol("App_Greeter__Greet_0", 0x2000, 20),
            ]);

        var owned = index.FindByAddress(0x1000);
        Assert.IsNotNull(owned);
        Assert.AreEqual("Run", owned!.Method.Name);
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, owned.Status);

        var shared = index.FindByAddress(0x2000);
        Assert.IsNotNull(shared);
        Assert.AreEqual("Greet", shared!.Method.Name);
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedAmbiguous, shared.Status);

        Assert.IsNull(index.FindByAddress(0xDEAD));
    }

    /// <summary>Verifies namespaced and nested declaring types join through sanitization.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_NamespacedAndNestedTypes_Join()
    {
        var index = Build(
            [new ManagedMethodSource("App",
            [
                Method("MyApp.Services.Greeter", "Run", 0x06000001),
                Method("MyApp.Outer/Inner", "Poke", 0x06000002),
            ])],
            [
                Symbol("App_MyApp_Services_Greeter__Run", 0x1000, 10),
                Symbol("App_MyApp_Outer_Inner__Poke", 0x2000, 12),
            ]);

        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, index.Find("App", 0x06000001)!.Status);
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, index.Find("App", 0x06000002)!.Status);
    }

    /// <summary>Verifies a Mach-O style leading underscore still joins.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_LeadingUnderscoreSymbol_Joins()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Run", 0x06000001)])],
            [Symbol("_App_Greeter__Run", 0x1000, 10)]);

        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, index.Find("App", 0x06000001)!.Status);
    }

    /// <summary>Verifies non-function symbols never join as methods.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_NonFunctionSymbols_Ignored()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Run", 0x06000001)])],
            [
                new NativeSymbol("App_Greeter__Run", null, 0x1000, null, null, ".data", 8,
                    NativeSymbolKind.Data, null, null, false, []),
            ]);

        Assert.AreEqual(MethodCorrelationStatus.NotInNativeImage, index.Find("App", 0x06000001)!.Status);
    }

    /// <summary>Verifies an address inside a correlated symbol (not just its entry) resolves.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FindByAddress_InsideSymbolRange_Resolves()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Run", 0x06000001)])],
            [Symbol("App_Greeter__Run", 0x1000, 0x40)]);

        Assert.AreEqual("Run", index.FindByAddress(0x1000)!.Method.Name);   // entry point
        Assert.AreEqual("Run", index.FindByAddress(0x1020)!.Method.Name);   // inside the body
        Assert.AreEqual("Run", index.FindByAddress(0x103F)!.Method.Name);   // last byte
        Assert.IsNull(index.FindByAddress(0x1040));                        // one past the end (exclusive)
        Assert.IsNull(index.FindByAddress(0x0FFF));                        // before the start
    }

    /// <summary>Verifies alias symbols sharing a virtual address contribute their size once.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_AliasSymbolsAtSameVa_SizeCountedOnce()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Run", 0x06000001)])],
            [
                Symbol("App_Greeter__Run", 0x1000, 16),
                Symbol("App_Greeter__Run", 0x1000, 16), // alias at the same VA
            ]);

        var run = index.Find("App", 0x06000001)!;
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, run.Status);
        Assert.AreEqual(16, run.NativeSize);
        Assert.AreEqual(16, index.TotalCorrelatedSize);
    }

    /// <summary>Verifies mstat rows repeating a dependency-graph node count their size once.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_RepeatedMstatNode_SizeCountedOnce()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Run", 0x06000001)])],
            [],
            Mstat(
                new MstatMethod("Run", "Greeter", "", "App", 30, 0, 0, "Greeter.Run()"),
                new MstatMethod("Run", "Greeter", "", "App", 30, 0, 0, "Greeter.Run()"))); // same node

        var run = index.Find("App", 0x06000001)!;
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedByMstatOnly, run.Status);
        Assert.AreEqual(30, run.NativeSize);
        Assert.AreEqual(30, index.TotalCorrelatedSize);
    }

    /// <summary>Verifies distinct mstat nodes (generic instantiations) each count their size.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_DistinctMstatNodes_SizesSum()
    {
        var index = Build(
            [new ManagedMethodSource("App", [Method("Greeter", "Describe", 0x06000001)])],
            [],
            Mstat(
                new MstatMethod("Describe<int>", "Greeter", "", "App", 30, 0, 0, "Greeter.Describe<int>()"),
                new MstatMethod("Describe<string>", "Greeter", "", "App", 40, 0, 0, "Greeter.Describe<string>()")));

        Assert.AreEqual(70, index.Find("App", 0x06000001)!.NativeSize);
        Assert.AreEqual(70, index.TotalCorrelatedSize);
    }
}
