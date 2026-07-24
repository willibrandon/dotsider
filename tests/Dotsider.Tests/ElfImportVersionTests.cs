using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Tests;

/// <summary>
/// Exercises GNU ELF symbol-version attribution, including malformed record graphs.
/// </summary>
[TestClass]
public sealed class ElfImportVersionTests
{
    private static readonly string[] AlphaFunctionNames = ["alpha_one", "alpha_two"];
    private static readonly string[] AllFunctionNames = ["alpha_one", "alpha_two", "beta_one"];
    private static readonly string[] BetaFunctionNames = ["beta_one"];

    private const uint ShtDynamic = 6;
    private const uint ShtDynSym = 11;
    private const uint ShtGnuVersionNeed = 0x6FFF_FFFE;
    private const uint ShtGnuVersionSymbol = 0x6FFF_FFFF;
    private const uint ShtStringTable = 3;

    /// <summary>
    /// Verifies a complete GNU version graph attributes each undefined symbol to the
    /// library named by its version requirement.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_ValidVersionRequirements_AttributesSymbolsToLibraries()
    {
        var imports = ElfImageReader.ReadImports(BuildImage(BuildValidRequirements(), 2));

        Assert.HasCount(2, imports);
        Assert.AreSequenceEqual(
            AlphaFunctionNames,
            FindModule(imports, "libalpha.so").Functions.Select(static function => function.Name));
        Assert.AreSequenceEqual(
            BetaFunctionNames,
            FindModule(imports, "libbeta.so").Functions.Select(static function => function.Name));
    }

    /// <summary>
    /// Verifies a backward <c>vn_next</c> edge cannot cycle between requirement records.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_CyclicVersionRequirementLinks_DegradesToUnversioned()
    {
        var strings = BuildDynamicStrings();
        var requirements = new byte[96];
        WriteRequirement(
            requirements,
            position: 0,
            auxiliaryCount: 1,
            FindString(strings, "libalpha.so"),
            auxiliaryOffset: 16,
            nextOffset: 32);
        WriteAuxiliary(
            requirements,
            position: 16,
            versionIndex: 2,
            FindString(strings, "VER_A"),
            nextOffset: 0);
        WriteRequirement(
            requirements,
            position: 32,
            auxiliaryCount: 1,
            FindString(strings, "libbeta.so"),
            auxiliaryOffset: 16,
            nextOffset: unchecked((uint)-32));
        WriteAuxiliary(
            requirements,
            position: 48,
            versionIndex: 4,
            FindString(strings, "VER_C"),
            nextOffset: 0);

        AssertSafeFallback(BuildImage(requirements, versionNeedCount: 3));
    }

    /// <summary>
    /// Verifies a backward <c>vna_next</c> edge cannot cycle between auxiliary records.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_CyclicVersionAuxiliaryLinks_DegradesToUnversioned()
    {
        var strings = BuildDynamicStrings();
        var requirements = new byte[64];
        WriteRequirement(
            requirements,
            position: 0,
            auxiliaryCount: 3,
            FindString(strings, "libalpha.so"),
            auxiliaryOffset: 16,
            nextOffset: 0);
        WriteAuxiliary(
            requirements,
            position: 16,
            versionIndex: 2,
            FindString(strings, "VER_A"),
            nextOffset: 16);
        WriteAuxiliary(
            requirements,
            position: 32,
            versionIndex: 3,
            FindString(strings, "VER_B"),
            nextOffset: unchecked((uint)-16));

        AssertSafeFallback(BuildImage(requirements, versionNeedCount: 1));
    }

    /// <summary>
    /// Verifies record links must be aligned, make progress, and leave a complete record
    /// inside the version-requirement section.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_InvalidVersionRecordOffsets_DegradesToUnversioned()
    {
        var misalignedRequirement = BuildValidRequirements();
        BinaryPrimitives.WriteUInt32LittleEndian(misalignedRequirement.AsSpan(12), 49);
        AssertSafeFallback(BuildImage(misalignedRequirement, 2));

        var overlappingAuxiliary = BuildValidRequirements();
        BinaryPrimitives.WriteUInt32LittleEndian(overlappingAuxiliary.AsSpan(8), 12);
        AssertSafeFallback(BuildImage(overlappingAuxiliary, 2));

        var outOfRangeAuxiliary = BuildValidRequirements();
        BinaryPrimitives.WriteUInt32LittleEndian(outOfRangeAuxiliary.AsSpan(28), 64);
        AssertSafeFallback(BuildImage(outOfRangeAuxiliary, 2));
    }

    /// <summary>
    /// Verifies declared record counts and zero terminators must describe the same graph.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_MismatchedCountsAndTerminators_DegradesToUnversioned()
    {
        var earlyRequirementTerminator = BuildValidRequirements();
        BinaryPrimitives.WriteUInt32LittleEndian(earlyRequirementTerminator.AsSpan(12), 0);
        AssertSafeFallback(BuildImage(earlyRequirementTerminator, 2));

        var trailingRequirementLink = BuildValidRequirements();
        BinaryPrimitives.WriteUInt32LittleEndian(trailingRequirementLink.AsSpan(60), 16);
        AssertSafeFallback(BuildImage(trailingRequirementLink, 2));

        var earlyAuxiliaryTerminator = BuildValidRequirements();
        BinaryPrimitives.WriteUInt32LittleEndian(earlyAuxiliaryTerminator.AsSpan(28), 0);
        AssertSafeFallback(BuildImage(earlyAuxiliaryTerminator, 2));
    }

    /// <summary>
    /// Verifies malformed record grammar and inconsistent section metadata invalidate
    /// version attribution without discarding the underlying imports.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_InvalidVersionGrammar_DegradesToUnversioned()
    {
        var unsupportedVersion = BuildValidRequirements();
        BinaryPrimitives.WriteUInt16LittleEndian(unsupportedVersion, 2);
        AssertSafeFallback(BuildImage(unsupportedVersion, 2));

        var zeroAuxiliaryCount = BuildValidRequirements();
        BinaryPrimitives.WriteUInt16LittleEndian(zeroAuxiliaryCount.AsSpan(2), 0);
        AssertSafeFallback(BuildImage(zeroAuxiliaryCount, 2));

        var reservedVersionIndex = BuildValidRequirements();
        BinaryPrimitives.WriteUInt16LittleEndian(reservedVersionIndex.AsSpan(22), 1);
        AssertSafeFallback(BuildImage(reservedVersionIndex, 2));

        var invalidVersionName = BuildValidRequirements();
        BinaryPrimitives.WriteUInt32LittleEndian(invalidVersionName.AsSpan(24), uint.MaxValue);
        AssertSafeFallback(BuildImage(invalidVersionName, 2));

        AssertSafeFallback(BuildImage(BuildValidRequirements()[..^1], 2));

        var wrongVersionType = BuildImage(BuildValidRequirements(), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(
            GetSectionHeader(wrongVersionType, 3)[4..],
            1);
        AssertSafeFallback(wrongVersionType);

        var wrongStringLink = BuildImage(BuildValidRequirements(), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(
            GetSectionHeader(wrongStringLink, 4)[40..],
            2);
        AssertSafeFallback(wrongStringLink);

        var oddVersionTableSize = BuildImage(BuildValidRequirements(), 2);
        BinaryPrimitives.WriteUInt64LittleEndian(
            GetSectionHeader(oddVersionTableSize, 3)[32..],
            7);
        AssertSafeFallback(oddVersionTableSize);
    }

    /// <summary>
    /// Verifies the requirement and cumulative auxiliary budgets accept their exact
    /// boundaries and reject the next record.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_VersionRecordBudgets_EnforceBoundaries()
    {
        var maximumRequirements = BuildRepeatedRequirements(
            [.. Enumerable.Repeat(1, 4_096)]);
        AssertVersionedAlpha(BuildImage(maximumRequirements, 4_096));

        var tooManyRequirements = BuildRepeatedRequirements(
            [.. Enumerable.Repeat(1, 4_097)]);
        AssertSafeFallback(BuildImage(tooManyRequirements, 4_097));

        var maximumAuxiliaries = BuildRepeatedRequirements([65_535, 1]);
        AssertVersionedAlpha(BuildImage(maximumAuxiliaries, 2));

        var tooManyAuxiliaries = BuildRepeatedRequirements([65_535, 2]);
        AssertSafeFallback(BuildImage(tooManyAuxiliaries, 2));
    }

    /// <summary>
    /// Verifies an attacker-controlled requirement count is rejected before traversal,
    /// even when the records themselves form a cycle.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_HugeCyclicRequirementCount_CompletesWithSafeFallback()
    {
        var strings = BuildDynamicStrings();
        var requirements = new byte[64];
        WriteRequirement(
            requirements,
            position: 0,
            auxiliaryCount: 1,
            FindString(strings, "libalpha.so"),
            auxiliaryOffset: 16,
            nextOffset: 32);
        WriteAuxiliary(
            requirements,
            position: 16,
            versionIndex: 5,
            FindString(strings, "VER_A"),
            nextOffset: 0);
        WriteRequirement(
            requirements,
            position: 32,
            auxiliaryCount: 1,
            FindString(strings, "libbeta.so"),
            auxiliaryOffset: 16,
            nextOffset: unchecked((uint)-32));
        WriteAuxiliary(
            requirements,
            position: 48,
            versionIndex: 6,
            FindString(strings, "VER_C"),
            nextOffset: 0);

        AssertSafeFallback(BuildImage(requirements, int.MaxValue));
    }

    /// <summary>
    /// Verifies the version-symbol table must contain exactly one entry for every dynamic
    /// symbol before any library attribution is trusted.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_MismatchedVersionSymbolTable_DegradesToUnversioned()
    {
        AssertSafeFallback(BuildImage(
            BuildValidRequirements(),
            versionNeedCount: 2,
            versionIndexes: [0, 2, 3]));

        AssertSafeFallback(BuildImage(
            BuildValidRequirements(),
            versionNeedCount: 2,
            versionIndexes: [0, 2, 3, 4, 5]));
    }

    /// <summary>
    /// Verifies string-table escapes and conflicting index ownership invalidate the complete
    /// attribution map rather than publishing a plausible prefix.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_InvalidStringsOrConflictingIndexes_DegradesTransactionally()
    {
        var invalidString = BuildValidRequirements();
        BinaryPrimitives.WriteUInt32LittleEndian(invalidString.AsSpan(4), uint.MaxValue);
        AssertSafeFallback(BuildImage(invalidString, 2));

        var strings = BuildDynamicStrings();
        var conflicting = BuildValidRequirements();
        BinaryPrimitives.WriteUInt16LittleEndian(conflicting.AsSpan(70), 2);
        AssertSafeFallback(BuildImage(conflicting, 2));

        var unterminatedStrings = new byte[strings.Length + 4];
        strings.CopyTo(unterminatedStrings, 0);
        "evil"u8.CopyTo(unterminatedStrings.AsSpan(strings.Length));
        var unterminated = BuildValidRequirements();
        BinaryPrimitives.WriteUInt32LittleEndian(
            unterminated.AsSpan(4),
            (uint)strings.Length);
        AssertSafeFallback(BuildImage(
            unterminated,
            versionNeedCount: 2,
            dynamicStrings: unterminatedStrings));
    }

    /// <summary>
    /// Verifies many undefined symbols share the precomputed map rather than walking the
    /// requirement graph independently.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadImports_ManySymbolsAndRequirements_CompletesWithExactResults()
    {
        const int symbolCount = 8_192;
        var requirements = BuildRepeatedRequirements(
            [.. Enumerable.Repeat(1, 4_096)]);
        var versionIndexes = new ushort[symbolCount + 1];
        Array.Fill(versionIndexes, (ushort)5, 1, symbolCount);
        var imports = ElfImageReader.ReadImports(BuildImage(
            requirements,
            versionNeedCount: 4_096,
            versionIndexes: versionIndexes,
            dynamicSymbolCount: symbolCount));

        var unversioned = FindModule(imports, "(unversioned)");
        Assert.HasCount(symbolCount, unversioned.Functions);
        Assert.IsEmpty(FindModule(imports, "libalpha.so").Functions);
        Assert.IsEmpty(FindModule(imports, "libbeta.so").Functions);
    }

    /// <summary>
    /// Verifies the public analyzer facade exposes the same deterministic fallback for a
    /// cyclic GNU version graph.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AssemblyAnalyzer_ImportsWithCyclicVersionGraph_ReturnsSafeFallback()
    {
        var requirements = BuildValidRequirements();
        BinaryPrimitives.WriteUInt32LittleEndian(
            requirements.AsSpan(60),
            unchecked((uint)-48));
        using var analyzer = new AssemblyAnalyzer(
            BuildImage(requirements, versionNeedCount: 3),
            "cyclic-version-records.elf");

        AssertSafeFallback(analyzer.Imports);
    }

    private static void AssertSafeFallback(byte[] image) =>
        AssertSafeFallback(ElfImageReader.ReadImports(image));

    private static void AssertSafeFallback(IReadOnlyList<ImportedModuleInfo> imports)
    {
        Assert.HasCount(3, imports);
        Assert.IsEmpty(FindModule(imports, "libalpha.so").Functions);
        Assert.IsEmpty(FindModule(imports, "libbeta.so").Functions);
        Assert.AreSequenceEqual(
            AllFunctionNames,
            FindModule(imports, "(unversioned)").Functions.Select(static function => function.Name));
    }

    private static void AssertVersionedAlpha(byte[] image)
    {
        var imports = ElfImageReader.ReadImports(image);

        Assert.HasCount(2, imports);
        Assert.AreSequenceEqual(
            AllFunctionNames,
            FindModule(imports, "libalpha.so").Functions.Select(static function => function.Name));
        Assert.IsEmpty(FindModule(imports, "libbeta.so").Functions);
    }

    private static byte[] BuildDynamicEntries(byte[] strings)
    {
        var entries = new byte[48];
        WriteDynamicEntry(entries, 0, tag: 1, FindString(strings, "libalpha.so"));
        WriteDynamicEntry(entries, 16, tag: 1, FindString(strings, "libbeta.so"));
        return entries;
    }

    private static byte[] BuildDynamicStrings() =>
        BuildStringTable(
            "libalpha.so",
            "libbeta.so",
            "alpha_one",
            "alpha_two",
            "beta_one",
            "VER_A",
            "VER_B",
            "VER_C");

    private static byte[] BuildDynamicSymbols(byte[] strings, int symbolCount)
    {
        var symbols = new byte[(symbolCount + 1) * 24];
        for (var i = 1; i <= symbolCount; i++)
        {
            var name = ((i - 1) % 3) switch
            {
                0 => "alpha_one",
                1 => "alpha_two",
                _ => "beta_one",
            };
            var position = i * 24;
            BinaryPrimitives.WriteUInt32LittleEndian(
                symbols.AsSpan(position),
                FindString(strings, name));
            symbols[position + 4] = 0x10;
        }

        return symbols;
    }

    private static byte[] BuildImage(
        byte[] versionRequirements,
        uint versionNeedCount,
        ushort[]? versionIndexes = null,
        byte[]? dynamicStrings = null,
        int dynamicSymbolCount = 3)
    {
        dynamicStrings ??= BuildDynamicStrings();
        versionIndexes ??= [0, 2, 0x8003, 4];
        var dynamicSymbols = BuildDynamicSymbols(dynamicStrings, dynamicSymbolCount);
        var versionSymbols = new byte[versionIndexes.Length * sizeof(ushort)];
        for (var i = 0; i < versionIndexes.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(
                versionSymbols.AsSpan(i * sizeof(ushort)),
                versionIndexes[i]);
        }

        return SyntheticImageBuilders.BuildElf(
            (".dynstr", 0, dynamicStrings, ShtStringTable, 0u, 0u, 0UL),
            (".dynsym", 0, dynamicSymbols, ShtDynSym, 1u, 0u, 0UL),
            (".gnu.version", 0, versionSymbols, ShtGnuVersionSymbol, 2u, 0u, 0UL),
            (".gnu.version_r", 0, versionRequirements, ShtGnuVersionNeed, 1u, versionNeedCount, 0UL),
            (".dynamic", 0, BuildDynamicEntries(dynamicStrings), ShtDynamic, 1u, 0u, 0UL));
    }

    private static byte[] BuildRepeatedRequirements(int[] auxiliaryCounts)
    {
        var strings = BuildDynamicStrings();
        var length = auxiliaryCounts.Sum(static count => 16 + count * 16);
        var requirements = new byte[length];
        var position = 0;
        var auxiliaryNumber = 0;
        for (var requirementIndex = 0; requirementIndex < auxiliaryCounts.Length;
            requirementIndex++)
        {
            var count = auxiliaryCounts[requirementIndex];
            var recordSize = 16 + count * 16;
            WriteRequirement(
                requirements,
                position,
                checked((ushort)count),
                FindString(strings, "libalpha.so"),
                auxiliaryOffset: 16,
                nextOffset: requirementIndex == auxiliaryCounts.Length - 1
                    ? 0
                    : (uint)recordSize);
            for (var auxiliaryIndex = 0; auxiliaryIndex < count; auxiliaryIndex++)
            {
                WriteAuxiliary(
                    requirements,
                    position + 16 + auxiliaryIndex * 16,
                    versionIndex: (ushort)(2 + auxiliaryNumber % 3),
                    FindString(strings, "VER_A"),
                    nextOffset: auxiliaryIndex == count - 1 ? 0u : 16u);
                auxiliaryNumber++;
            }

            position += recordSize;
        }

        return requirements;
    }

    private static byte[] BuildStringTable(params string[] values)
    {
        var result = new List<byte> { 0 };
        foreach (var value in values)
        {
            result.AddRange(Encoding.UTF8.GetBytes(value));
            result.Add(0);
        }

        return [.. result];
    }

    private static byte[] BuildValidRequirements()
    {
        var strings = BuildDynamicStrings();
        var requirements = new byte[80];
        WriteRequirement(
            requirements,
            position: 0,
            auxiliaryCount: 2,
            FindString(strings, "libalpha.so"),
            auxiliaryOffset: 16,
            nextOffset: 48);
        WriteAuxiliary(
            requirements,
            position: 16,
            versionIndex: 2,
            FindString(strings, "VER_A"),
            nextOffset: 16);
        WriteAuxiliary(
            requirements,
            position: 32,
            versionIndex: 3,
            FindString(strings, "VER_B"),
            nextOffset: 0);
        WriteRequirement(
            requirements,
            position: 48,
            auxiliaryCount: 1,
            FindString(strings, "libbeta.so"),
            auxiliaryOffset: 16,
            nextOffset: 0);
        WriteAuxiliary(
            requirements,
            position: 64,
            versionIndex: 4,
            FindString(strings, "VER_C"),
            nextOffset: 0);
        return requirements;
    }

    private static uint FindString(byte[] table, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var offset = table.AsSpan().IndexOf(bytes);
        Assert.IsGreaterThanOrEqualTo(0, offset);
        return (uint)offset;
    }

    private static ImportedModuleInfo FindModule(
        IReadOnlyList<ImportedModuleInfo> imports,
        string name) =>
        Assert.ContainsSingle(
            module => string.Equals(module.ModuleName, name, StringComparison.Ordinal),
            imports);

    private static Span<byte> GetSectionHeader(byte[] image, int sectionIndex)
    {
        var tableOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(
            image.AsSpan(40)));
        return image.AsSpan(tableOffset + sectionIndex * 64, 64);
    }

    private static void WriteAuxiliary(
        byte[] requirements,
        int position,
        ushort versionIndex,
        uint nameOffset,
        uint nextOffset)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(
            requirements.AsSpan(position + 6),
            versionIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(
            requirements.AsSpan(position + 8),
            nameOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(
            requirements.AsSpan(position + 12),
            nextOffset);
    }

    private static void WriteDynamicEntry(
        byte[] entries,
        int position,
        ulong tag,
        uint value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(entries.AsSpan(position), tag);
        BinaryPrimitives.WriteUInt64LittleEndian(entries.AsSpan(position + 8), value);
    }

    private static void WriteRequirement(
        byte[] requirements,
        int position,
        ushort auxiliaryCount,
        uint fileNameOffset,
        uint auxiliaryOffset,
        uint nextOffset)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(requirements.AsSpan(position), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(
            requirements.AsSpan(position + 2),
            auxiliaryCount);
        BinaryPrimitives.WriteUInt32LittleEndian(
            requirements.AsSpan(position + 4),
            fileNameOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(
            requirements.AsSpan(position + 8),
            auxiliaryOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(
            requirements.AsSpan(position + 12),
            nextOffset);
    }
}
