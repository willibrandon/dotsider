using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.Wasm;
using Dotsider.Core.Protocol;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Verifies WebAssembly count, containment, and aggregate-work defenses with synthetic modules.
/// </summary>
[TestClass]
public sealed class WasmModuleReaderTests
{
    /// <summary>
    /// Every standard section vector rejects an impossible count before allocating its result.
    /// </summary>
    /// <param name="sectionId">The standard WebAssembly section identifier.</param>
    /// <param name="sectionName">The expected diagnostic context.</param>
    [TestMethod]
    [DataRow((byte)1, "type-section")]
    [DataRow((byte)2, "import-section")]
    [DataRow((byte)3, "function-section")]
    [DataRow((byte)4, "table-section")]
    [DataRow((byte)5, "memory-section")]
    [DataRow((byte)6, "global-section")]
    [DataRow((byte)7, "export-section")]
    [DataRow((byte)9, "element-section")]
    [DataRow((byte)10, "code-section")]
    [DataRow((byte)11, "data-section")]
    [DataRow((byte)13, "tag-section")]
    public void StandardSectionVector_ImpossibleCount_FailsWithoutResults(
        byte sectionId, string sectionName)
    {
        byte[] module = BuildModule(BuildSection(sectionId, EncodeU32(uint.MaxValue)));

        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);

        Assert.IsNotNull(result.Diagnostic);
        Assert.Contains(sectionName, result.Diagnostic);
        Assert.IsEmpty(result.Sections);
        Assert.IsEmpty(result.Types);
        Assert.IsEmpty(result.Imports);
        Assert.IsEmpty(result.Functions);
        Assert.IsEmpty(result.DataSegments);
    }

    /// <summary>
    /// A tiny module declaring billions of entries does not perform a count-sized allocation.
    /// </summary>
    [TestMethod]
    public void StandardSectionVector_HugeCount_DoesNotAllocateFromDeclaredCount()
    {
        byte[] module = BuildModule(BuildSection(1, EncodeU32(uint.MaxValue)));
        _ = WasmModuleReader.Read(module, filePath: null);

        long before = GC.GetAllocatedBytesForCurrentThread();
        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.IsNotNull(result.Diagnostic);
        Assert.IsLessThan(64 * 1024L, allocated);
    }

    /// <summary>
    /// Nested type, local, and element vectors apply the same pre-allocation byte checks.
    /// </summary>
    /// <param name="sectionId">The containing section identifier.</param>
    /// <param name="payload">The section payload with an impossible nested count.</param>
    /// <param name="description">The expected nested-vector diagnostic context.</param>
    [TestMethod]
    [DynamicData(nameof(NestedVectorCases))]
    public void NestedVector_ImpossibleCount_FailsWithinContainingRegion(
        byte sectionId, byte[] payload, string description)
    {
        byte[] module = BuildModule(BuildSection(sectionId, payload));

        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);

        Assert.IsNotNull(result.Diagnostic);
        Assert.Contains(description, result.Diagnostic);
        Assert.IsEmpty(result.Sections);
    }

    /// <summary>
    /// Corrupt descriptive custom vectors are ignored without hiding later executable sections.
    /// </summary>
    /// <param name="name">The custom-section name.</param>
    /// <param name="customData">The corrupt payload after the custom-section name.</param>
    [TestMethod]
    [DynamicData(nameof(CustomVectorCases))]
    public void CustomSectionVector_ImpossibleCount_PreservesLaterStandardSections(
        string name, byte[] customData)
    {
        byte[] customPayload = [.. EncodeName(name), .. customData];
        byte[] module = BuildModule(
            BuildSection(0, customPayload),
            BuildSection(1, [0x01, 0x60, 0x00, 0x00]),
            BuildSection(3, [0x01, 0x00]),
            BuildSection(10, [0x01, 0x02, 0x00, 0x0B]));

        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);

        Assert.IsNull(result.Diagnostic);
        Assert.HasCount(4, result.Sections);
        Assert.HasCount(1, result.Types);
        Assert.HasCount(1, result.Functions);
        Assert.AreEqual(1, result.DefinedFunctionCount);
    }

    /// <summary>
    /// A malformed import section publishes none of the entries decoded before its failure.
    /// </summary>
    [TestMethod]
    public void ImportSection_InvalidLaterEntry_DiscardsPartialSectionResults()
    {
        byte[] payload =
        [
            0x02,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0xFF, 0x00
        ];
        byte[] module = BuildModule(BuildSection(2, payload));

        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);

        Assert.IsNotNull(result.Diagnostic);
        Assert.Contains("import kind", result.Diagnostic);
        Assert.IsEmpty(result.Imports);
        Assert.IsEmpty(result.Functions);
    }

    /// <summary>
    /// A malformed descriptive section publishes none of the entries decoded before its failure.
    /// </summary>
    [TestMethod]
    public void CustomSection_InvalidLaterEntry_DiscardsPartialSectionResults()
    {
        byte[] targetFeatures =
        [
            .. EncodeName("target_features"),
            0x02,
            (byte)'+', 0x00,
            (byte)'-', 0x01
        ];
        byte[] module = BuildModule(
            BuildSection(0, targetFeatures),
            BuildSection(3, [0x00]));

        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);

        Assert.IsNull(result.Diagnostic);
        Assert.IsEmpty(result.TargetFeatures);
        Assert.HasCount(2, result.Sections);
    }

    /// <summary>
    /// The shared budget accepts its exact boundary across an outer and nested element vector.
    /// </summary>
    [TestMethod]
    public void SharedItemBudget_ExactBoundary_IsAccepted()
    {
        int nestedCount = WasmModuleReader.MaxDecodedItems - 2;
        byte[] payload =
        [
            0x01,
            0x01,
            0x00,
            .. EncodeU32((uint)nestedCount),
            .. new byte[nestedCount]
        ];
        byte[] module = BuildModule(BuildSection(9, payload));

        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);

        Assert.IsNull(result.Diagnostic);
        WasmElementSegmentInfo element = Assert.ContainsSingle(result.Elements);
        Assert.AreEqual(nestedCount, element.ElementCount);
    }

    /// <summary>
    /// The shared budget rejects one additional nested item before walking the vector.
    /// </summary>
    [TestMethod]
    public void SharedItemBudget_OnePastBoundary_IsRejected()
    {
        int nestedCount = WasmModuleReader.MaxDecodedItems - 1;
        byte[] payload =
        [
            0x01,
            0x01,
            0x00,
            .. EncodeU32((uint)nestedCount),
            .. new byte[nestedCount]
        ];
        byte[] module = BuildModule(BuildSection(9, payload));

        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);

        Assert.IsNotNull(result.Diagnostic);
        Assert.Contains("1,048,576-item decoding budget", result.Diagnostic);
        Assert.IsEmpty(result.Elements);
    }

    /// <summary>
    /// A legal non-minimal five-byte count is accepted through the complete module parser.
    /// </summary>
    [TestMethod]
    public void SectionCount_LegalNonMinimalFiveByteZero_IsAccepted()
    {
        byte[] module = BuildModule(
            BuildSection(3, [0x80, 0x80, 0x80, 0x80, 0x00]));

        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);

        Assert.IsNull(result.Diagnostic);
        Assert.HasCount(1, result.Sections);
        Assert.IsEmpty(result.Functions);
    }

    /// <summary>
    /// A function body cannot satisfy its local vector with bytes from the next body.
    /// </summary>
    [TestMethod]
    public void FunctionBody_LocalVectorCrossesBodyBoundary_FailsClosed()
    {
        byte[] codePayload =
        [
            0x02,
            0x01, 0x01,
            0x02, 0x00, 0x0B,
            0x00
        ];
        byte[] module = BuildModule(BuildSection(10, codePayload));

        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);

        Assert.IsNotNull(result.Diagnostic);
        Assert.Contains("function-local declaration", result.Diagnostic);
        Assert.IsEmpty(result.Functions);
        Assert.IsEmpty(result.Sections);
    }

    /// <summary>
    /// A compressed local run cannot expand beyond the shared structured-item budget.
    /// </summary>
    [TestMethod]
    public void FunctionBody_ExpandedLocalCountExceedsBudget_FailsClosed()
    {
        byte[] body =
        [
            0x01,
            .. EncodeU32((uint)WasmModuleReader.MaxDecodedItems),
            0x7F,
            0x0B
        ];
        byte[] codePayload =
        [
            0x01,
            .. EncodeU32((uint)body.Length),
            .. body
        ];
        byte[] module = BuildModule(BuildSection(10, codePayload));

        WasmModuleInfo result = WasmModuleReader.Read(module, filePath: null);

        Assert.IsNotNull(result.Diagnostic);
        Assert.Contains("function local", result.Diagnostic);
        Assert.Contains("1,048,576-item decoding budget", result.Diagnostic);
        Assert.IsEmpty(result.Functions);
        Assert.IsEmpty(result.Sections);
    }

    /// <summary>
    /// A malformed later section retains previously decoded facts and surfaces the diagnostic
    /// through both the public analyzer and JSON-ready summary facade.
    /// </summary>
    [TestMethod]
    public void AssemblyAnalyzer_MalformedLaterVector_RetainsPrefixAndSurfacesDiagnostic()
    {
        byte[] module = BuildModule(
            BuildSection(1, [0x01, 0x60, 0x00, 0x00]),
            BuildSection(3, EncodeU32(uint.MaxValue)));

        using var analyzer = new AssemblyAnalyzer(module, "malformed-count.wasm");

        Assert.AreEqual(BinaryKind.Wasm, analyzer.BinaryKind);
        WasmModuleInfo? wasm = analyzer.WasmModuleInfo;
        Assert.IsNotNull(wasm);
        Assert.HasCount(1, wasm.Types);
        Assert.IsEmpty(wasm.Functions);
        Assert.IsNotNull(wasm.Diagnostic);
        Assert.Contains("function-section", wasm.Diagnostic);

        string json = JsonSerializer.Serialize(
            WasmPayloadBuilder.BuildSummary(analyzer), DotsiderJsonOptions.Default);
        JsonElement summary = JsonSerializer.Deserialize<JsonElement>(json);
        string? diagnostic = summary.GetProperty("diagnostic").GetString();
        Assert.IsNotNull(diagnostic);
        Assert.Contains("function-section", diagnostic);
    }

    /// <summary>
    /// Gets corrupt custom-section vectors that must not suppress later standard sections.
    /// </summary>
    /// <returns>The custom-section name and malformed payload for each case.</returns>
    public static IEnumerable<object[]> CustomVectorCases()
    {
        yield return
        [
            "name",
            new byte[] { 0x01, 0x05, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }
        ];
        yield return
        [
            "producers",
            EncodeU32(uint.MaxValue)
        ];
        yield return
        [
            "target_features",
            EncodeU32(uint.MaxValue)
        ];
    }

    /// <summary>
    /// Gets nested standard vectors whose declared counts exceed their containing region.
    /// </summary>
    /// <returns>The section identifier, payload, and diagnostic context for each case.</returns>
    public static IEnumerable<object[]> NestedVectorCases()
    {
        yield return
        [
            (byte)1,
            new byte[] { 0x01, 0x60, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F },
            "parameter"
        ];
        yield return
        [
            (byte)1,
            new byte[] { 0x01, 0x60, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F },
            "result"
        ];
        yield return
        [
            (byte)9,
            new byte[] { 0x01, 0x01, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F },
            "element function-index"
        ];
        yield return
        [
            (byte)9,
            new byte[] { 0x01, 0x05, 0x70, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F },
            "element expression"
        ];
        yield return
        [
            (byte)10,
            new byte[] { 0x01, 0x05, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F },
            "function-local declaration"
        ];
    }

    private static byte[] BuildModule(params byte[][] sections) =>
    [
        0x00, 0x61, 0x73, 0x6D,
        0x01, 0x00, 0x00, 0x00,
        .. sections.SelectMany(static section => section)
    ];

    private static byte[] BuildSection(byte id, byte[] payload) =>
    [
        id,
        .. EncodeU32((uint)payload.Length),
        .. payload
    ];

    private static byte[] EncodeName(string value)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return [.. EncodeU32((uint)bytes.Length), .. bytes];
    }

    private static byte[] EncodeU32(uint value)
    {
        var bytes = new List<byte>(5);
        do
        {
            byte current = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
                current |= 0x80;
            bytes.Add(current);
        } while (value != 0);

        return [.. bytes];
    }
}
