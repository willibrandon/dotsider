using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.ReadyToRun;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Verifies malformed image-wide ReadyToRun tables fail closed through the public analysis
/// surfaces while valid real images retain their method maps.
/// </summary>
[TestClass]
public sealed class ReadyToRunTableProductionTests
{
    private const string CompositeSkipReason = "ReadyToRun composite publish did not run on this leg.";
    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies malformed real runtime-function sections preserve header and metadata inspection
    /// while making the method map explicitly unavailable.
    /// </summary>
    /// <param name="malformation">The runtime-function section malformation.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("NegativeSize")]
    [DataRow("PartialRecord")]
    [DataRow("AboveBudget")]
    public void RuntimeFunctions_MalformedRealTable_DisablesMethodMap(string malformation)
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var baseline = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);
        var baselineInfo = baseline.ReadyToRunInfo;
        Assert.IsNotNull(baselineInfo);
        var recordSize = baselineInfo.Architecture == NativeArchitecture.X64 ? 12 : 8;
        var declaredSize = malformation switch
        {
            "NegativeSize" => -1,
            "PartialRecord" => 1,
            "AboveBudget" => checked(
                (ReadyToRunRuntimeFunctionTable.MaxRuntimeFunctionCount + 1) * recordSize),
            _ => throw new ArgumentOutOfRangeException(nameof(malformation)),
        };
        var (Image, PayloadOffset) = ReadyToRunImagePatcher.PatchImageWideTable(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunSectionType.RuntimeFunctions,
            [0],
            declaredSize);

        using var analyzer = new AssemblyAnalyzer(Image, Samples.ReadyToRunConsoleDll!);

        AssertMethodMapUnavailable(analyzer, "RuntimeFunctions");
        Assert.Contains(
            section => section.SectionId == (int)ReadyToRunSectionType.RuntimeFunctions,
            analyzer.ReadyToRunSections);
    }

    /// <summary>
    /// Verifies a malformed hot/cold table patched into a real crossgen2 image disables the complete
    /// method map and retains the rest of the ReadyToRun model.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HotColdMap_MalformedRealTable_DisablesMethodMap()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var (Image, PayloadOffset) = ReadyToRunImagePatcher.PatchImageWideTable(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunSectionType.HotColdMap,
            new byte[8],
            8,
            ReadyToRunSectionType.CrossModuleInlineInfo);

        using var analyzer = new AssemblyAnalyzer(Image, Samples.ReadyToRunConsoleDll!);

        AssertMethodMapUnavailable(analyzer, "HotColdMap");
        Assert.Contains(
            section => section.SectionId == (int)ReadyToRunSectionType.HotColdMap,
            analyzer.ReadyToRunSections);
    }

    /// <summary>
    /// Verifies a valid hot/cold pair reaches the real method builder and adds the exact compact
    /// cold range to its owning hot method.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HotColdMap_ValidRealTable_AddsColdRange()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        int hot;
        int cold;
        int expectedColdStartRva;
        using (var baseline = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!))
        {
            var info = baseline.ReadyToRunInfo;
            Assert.IsNotNull(info);
            var section = info.Sections.Single(
                static candidate => candidate.Type == (int)ReadyToRunSectionType.RuntimeFunctions);
            Assert.IsNotNull(section.FileOffset);
            var addressSpace = NativeAddressSpace.Create(baseline.RawBytes.Span);
            Assert.IsNotNull(addressSpace);
            var valid = ReadyToRunRuntimeFunctionTable.TryRead(
                new R2RNativeReader(baseline.RawBytes),
                section.FileOffset.Value,
                section.Size,
                info.Architecture,
                baseline.PeHeaders?.ImageBase ?? 0,
                addressSpace,
                out var table,
                out var diagnostic);
            Assert.IsTrue(valid, diagnostic);
            Assert.IsNotNull(table);

            cold = table.Count - 1;
            hot = baseline.ReadyToRunMethods
                .Select(static method => method.EntryPointRuntimeFunctionId)
                .First(id => id >= 0 && id < cold);
            expectedColdStartRva = table.StartRva(cold);
        }

        var pair = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(pair, cold);
        BinaryPrimitives.WriteInt32LittleEndian(pair.AsSpan(4), hot);
        var (Image, PayloadOffset) = ReadyToRunImagePatcher.PatchImageWideTable(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunSectionType.HotColdMap,
            pair,
            pair.Length,
            ReadyToRunSectionType.CrossModuleInlineInfo);

        using var analyzer = new AssemblyAnalyzer(Image, Samples.ReadyToRunConsoleDll!);
        var method = analyzer.ReadyToRunMethods.First(
            candidate => candidate.EntryPointRuntimeFunctionId == hot);
        var coldRange = Assert.ContainsSingle(
            range => range.Kind == ReadyToRunCodeRangeKind.Cold,
            method.CodeRanges);

        Assert.AreEqual(expectedColdStartRva, coldRange.StartRva);
        Assert.IsNotNull(analyzer.ReadyToRunIndex);
        var symbols = analyzer.NativeSymbols;
        Assert.IsNotNull(symbols);
        Assert.AreEqual(NativeSymbolStatus.Loaded, symbols.Status);
    }

    /// <summary>
    /// Verifies a large real composite image remains below the accepted runtime-function budget and
    /// exposes its complete method index.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RuntimeFunctions_LargeRealComposite_RetainsMethodMap()
    {
        TestSkip.When(Samples.ReadyToRunCompositeImage is null, CompositeSkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunCompositeImage!);
        var info = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(info);
        var section = info.Sections.Single(
            static candidate => candidate.Type == (int)ReadyToRunSectionType.RuntimeFunctions);
        var recordSize = info.Architecture == NativeArchitecture.X64 ? 12 : 8;
        var runtimeFunctionCount = section.Size / recordSize;

        Assert.AreEqual(ReadyToRunStatus.Valid, info.Status);
        Assert.IsGreaterThan(200_000, runtimeFunctionCount);
        Assert.IsLessThanOrEqualTo(
            ReadyToRunRuntimeFunctionTable.MaxRuntimeFunctionCount,
            runtimeFunctionCount);
        Assert.IsNotEmpty(analyzer.ReadyToRunMethods);
        Assert.IsNotNull(analyzer.ReadyToRunIndex);
        var symbols = analyzer.NativeSymbols;
        Assert.IsNotNull(symbols);
        Assert.AreEqual(NativeSymbolStatus.Loaded, symbols.Status);
    }

    private static void AssertMethodMapUnavailable(AssemblyAnalyzer analyzer, string tableName)
    {
        var info = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(info);
        Assert.AreEqual(ReadyToRunStatus.Valid, info.Status);
        Assert.IsTrue(analyzer.HasMetadata);
        Assert.IsNotEmpty(analyzer.MethodDefs);
        Assert.IsEmpty(analyzer.ReadyToRunMethods);
        Assert.IsNull(analyzer.ReadyToRunIndex);

        var symbols = analyzer.NativeSymbols;
        Assert.IsNotNull(symbols);
        Assert.AreEqual(NativeSymbolStatus.CorruptSymbolFile, symbols.Status);
        Assert.Contains(tableName, symbols.Diagnostic!);

        var result = ReadyToRunCorrelationQuery.Resolve(analyzer, "Greeter.Greet", CancellationToken.None);
        Assert.AreEqual(ReadyToRunQueryOutcome.Unavailable, result.Outcome);
        Assert.Contains(tableName, result.Message!);
    }
}
