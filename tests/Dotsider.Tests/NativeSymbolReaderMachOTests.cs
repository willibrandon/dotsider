using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the Mach-O arm of <see cref="NativeSymbolReader"/>: every probe outcome with
/// synthetic images on all platforms — nlist-only, merged dSYM DWARF+nlist, UUID mismatch, fat
/// slice selection and ambiguity, and the <c>LC_FUNCTION_STARTS</c> fallback chain — plus the
/// real NativeAOT fixture on the macOS leg (where its symbol file is a dSYM).
/// </summary>
[TestClass]
public class NativeSymbolReaderMachOTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const uint ExecFlags = 0x8000_0400;
    private const byte SectType = 0x0E;

    private static readonly byte[] UuidA = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
    private static readonly byte[] UuidB = [16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1];
    private static readonly byte[] UuidC = [9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9];

    private static (string, ulong, (string, ulong, uint, byte[])[]) TextSegment() =>
        ("__TEXT", 0x1_0000_0000, new[] { ("__text", 0x1_0000_1000UL, ExecFlags, new byte[0x100]) });

    /// <summary>Builds one-function .debug_info/.debug_abbrev blobs (v4, name + low_pc + size).</summary>
    private static (byte[] Info, byte[] Abbrev) MinimalDwarf(string name, ulong lowPc, uint size)
    {
        var abbrev = new DwarfBlob()
            .ULeb(1).ULeb(0x11).U8(1).ULeb(0).ULeb(0)
            .ULeb(2).ULeb(0x2E).U8(0)
            .ULeb(0x03).ULeb(0x08).ULeb(0x11).ULeb(0x01).ULeb(0x12).ULeb(0x06)
            .ULeb(0).ULeb(0)
            .ULeb(0);
        var dies = new DwarfBlob()
            .ULeb(1)
            .ULeb(2).CStr(name).U64(lowPc).U32(size)
            .ULeb(0);
        var body = new DwarfBlob().U16(4).U32(0).U8(8).Bytes(dies.ToArray());
        return (new DwarfBlob().U32((uint)body.Length).Bytes(body.ToArray()).ToArray(), abbrev.ToArray());
    }

    private static string WriteDsym(string directory, string imageName, byte[] innerBytes)
    {
        var dwarfDir = Path.Combine(directory, imageName + ".dSYM", "Contents", "Resources", "DWARF");
        Directory.CreateDirectory(dwarfDir);
        var inner = Path.Combine(dwarfDir, imageName);
        File.WriteAllBytes(inner, innerBytes);
        return inner;
    }

    /// <summary>
    /// Verifies an image without a dSYM loads its own nlist as a named primary source.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_NlistOnly_LoadsFromImage()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-macho-");
        try
        {
            var image = SyntheticImageBuilders.BuildMachO(
                [TextSegment()],
                symbols: [("_frost_main", SectType, 1, 0x1_0000_1010)]);
            var path = Path.Combine(dir.FullName, "app");
            File.WriteAllBytes(path, image);

            var result = NativeSymbolReader.Read(path, File.ReadAllBytes(path), []);

            Assert.AreEqual(NativeSymbolSource.MachONlist, result.Source);
            Assert.AreEqual(NativeSymbolStatus.Loaded, result.Status);
            Assert.AreEqual(path, result.Path);
            var symbol = Assert.ContainsSingle(result.Symbols);
            Assert.AreEqual("frost_main", symbol.Name);
            Assert.AreEqual("__text", symbol.Section);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a UUID-matched dSYM merges its DWARF functions and its nlist data symbols, with
    /// file offsets re-anchored onto the analyzed image.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_MatchingDsym_MergesDwarfAndNlist()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-macho-");
        try
        {
            var image = SyntheticImageBuilders.BuildMachO(
                [
                    TextSegment(),
                    ("__DATA", 0x1_0000_4000, new[] { ("__const", 0x1_0000_4000UL, 0u, new byte[0x100]) }),
                ],
                uuid: UuidA);
            var path = Path.Combine(dir.FullName, "app");
            File.WriteAllBytes(path, image);

            var (info, abbrev) = MinimalDwarf("frost_main", 0x1_0000_1010, 0x40);
            var inner = SyntheticImageBuilders.BuildMachO(
                [
                    ("__DWARF", 0x2_0000_0000, new[]
                    {
                        ("__debug_info", 0x2_0000_0000UL, 0u, info),
                        ("__debug_abbrev", 0x2_0000_1000UL, 0u, abbrev),
                    }),
                    ("__DATA", 0x1_0000_4000, new[] { ("__const", 0x1_0000_4000UL, 0u, new byte[0x100]) }),
                ],
                symbols: [("__ZTV6Widget", SectType, 3, 0x1_0000_4010)],
                uuid: UuidA);
            var dsymPath = WriteDsym(dir.FullName, "app", inner);

            var result = NativeSymbolReader.Read(path, File.ReadAllBytes(path), []);

            Assert.AreEqual(NativeSymbolSource.Dsym, result.Source);
            Assert.AreEqual(NativeSymbolStatus.Loaded, result.Status);
            Assert.AreEqual(dsymPath, result.Path);
            Assert.IsNull(result.Diagnostic);

            var function = Assert.ContainsSingle(s => s.Kind == NativeSymbolKind.Function, result.Symbols);
            Assert.AreEqual("frost_main", function.Name);
            Assert.AreEqual(0x40, function.Size);
            Assert.AreEqual("__text", function.Section); // mapped through the image, not the dSYM
            Assert.IsNotNull(function.FileOffset);

            var data = Assert.ContainsSingle(s => s.Kind == NativeSymbolKind.MethodTable, result.Symbols);
            Assert.AreEqual("_ZTV6Widget", data.Name);
            Assert.AreEqual("__const", data.Section); // the dSYM's nlist, re-anchored on the image
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a dSYM whose UUID differs is rejected as
    /// <see cref="NativeSymbolStatus.IdMismatch"/>, with boundaries recovered from
    /// <c>LC_FUNCTION_STARTS</c>.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_MismatchedDsym_ReportsIdMismatch()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-macho-");
        try
        {
            var image = SyntheticImageBuilders.BuildMachO(
                [TextSegment()],
                uuid: UuidA,
                functionStarts: new DwarfBlob().ULeb(0x1010).ULeb(0).ToArray());
            var path = Path.Combine(dir.FullName, "app");
            File.WriteAllBytes(path, image);

            var (info, abbrev) = MinimalDwarf("fn", 0x1_0000_1010, 0x10);
            WriteDsym(dir.FullName, "app", SyntheticImageBuilders.BuildMachO(
                [("__DWARF", 0x2_0000_0000, new[] { ("__debug_info", 0x2_0000_0000UL, 0u, info), ("__debug_abbrev", 0x2_0000_1000UL, 0u, abbrev) })],
                uuid: UuidB));

            var result = NativeSymbolReader.Read(path, File.ReadAllBytes(path), []);

            Assert.AreEqual(NativeSymbolStatus.IdMismatch, result.Status);
            Assert.AreEqual(NativeSymbolSource.FunctionStartsFallback, result.Source);
            Assert.Contains("UUID", result.Diagnostic!);
            var boundary = Assert.ContainsSingle(result.Symbols);
            Assert.AreEqual(NativeSymbolKind.Boundary, boundary.Kind);
            Assert.AreEqual(0xF0, boundary.Size); // clamped to __text's end
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies the symbol-free chain: <c>LC_FUNCTION_STARTS</c> boundaries as
    /// <see cref="NativeSymbolStatus.FallbackOnly"/>, then
    /// <see cref="NativeSymbolStatus.NoSymbolFile"/> when nothing at all is present.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_NoSymbols_FallsBackToFunctionStartsThenNoSymbolFile()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-macho-");
        try
        {
            var withStarts = Path.Combine(dir.FullName, "app");
            File.WriteAllBytes(withStarts, SyntheticImageBuilders.BuildMachO(
                [TextSegment()],
                functionStarts: new DwarfBlob().ULeb(0x1010).ULeb(0x40).ULeb(0).ToArray()));
            var result = NativeSymbolReader.Read(withStarts, File.ReadAllBytes(withStarts), []);

            Assert.AreEqual(NativeSymbolStatus.FallbackOnly, result.Status);
            Assert.AreEqual(NativeSymbolSource.FunctionStartsFallback, result.Source);
            Assert.IsNotEmpty(result.Symbols);

            var bare = Path.Combine(dir.FullName, "bare");
            File.WriteAllBytes(bare, SyntheticImageBuilders.BuildMachO([TextSegment()]));
            var empty = NativeSymbolReader.Read(bare, File.ReadAllBytes(bare), []);

            Assert.AreEqual(NativeSymbolStatus.NoSymbolFile, empty.Status);
            Assert.IsEmpty(empty.Symbols);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies fat handling: the dSYM's UUID picks its slice (with file offsets shifted to the
    /// archive), and an undisambiguated archive reports
    /// <see cref="NativeSymbolStatus.AmbiguousImage"/> naming the slices found.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FatArchive_UuidSelectsSliceOrAmbiguous()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-macho-");
        try
        {
            var matching = SyntheticImageBuilders.BuildMachO([TextSegment()], uuid: UuidA);
            var other = SyntheticImageBuilders.BuildMachO([TextSegment()], uuid: UuidB, cpuType: 0x0100_0007);
            var fat = SyntheticImageBuilders.BuildFat(other, matching); // match is the second slice
            var path = Path.Combine(dir.FullName, "app");
            File.WriteAllBytes(path, fat);

            var (info, abbrev) = MinimalDwarf("sliced_fn", 0x1_0000_1010, 0x20);
            WriteDsym(dir.FullName, "app", SyntheticImageBuilders.BuildMachO(
                [("__DWARF", 0x2_0000_0000, new[] { ("__debug_info", 0x2_0000_0000UL, 0u, info), ("__debug_abbrev", 0x2_0000_1000UL, 0u, abbrev) })],
                uuid: UuidA));

            var result = NativeSymbolReader.Read(path, File.ReadAllBytes(path), []);

            Assert.AreEqual(NativeSymbolStatus.Loaded, result.Status);
            var symbol = Assert.ContainsSingle(s => s.Name == "sliced_fn", result.Symbols);
            var slices = MachOImageReader.ReadFatSlices(fat);
            Assert.IsNotNull(symbol.FileOffset);
            Assert.IsGreaterThan(slices[1].Offset, symbol.FileOffset.Value, "the file offset must be shifted into the chosen slice's archive region");

            // No dSYM, no AOT signal: ambiguous, deterministically.
            var bare = Path.Combine(dir.FullName, "bare");
            File.WriteAllBytes(bare, fat);
            var ambiguous = NativeSymbolReader.Read(bare, File.ReadAllBytes(bare), []);

            Assert.AreEqual(NativeSymbolStatus.AmbiguousImage, ambiguous.Status);
            Assert.IsEmpty(ambiguous.Symbols);
            Assert.Contains("0x100000c", ambiguous.Diagnostic!);
            Assert.Contains("0x1000007", ambiguous.Diagnostic!);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a universal (fat) dSYM is sliced before UUID validation: the slice carrying the
    /// image's UUID is selected and its DWARF read, instead of the fat header failing the
    /// identity check.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FatDsym_SlicesToMatchingUuid()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-fatdsym-");
        try
        {
            var path = Path.Combine(dir.FullName, "app");
            File.WriteAllBytes(path, SyntheticImageBuilders.BuildMachO([TextSegment()], uuid: UuidA));

            var (info, abbrev) = MinimalDwarf("frost_main", 0x1_0000_1010, 0x40);
            var matching = SyntheticImageBuilders.BuildMachO(
                [("__DWARF", 0x2_0000_0000, new[]
                {
                    ("__debug_info", 0x2_0000_0000UL, 0u, info),
                    ("__debug_abbrev", 0x2_0000_1000UL, 0u, abbrev),
                })],
                uuid: UuidA);
            var other = SyntheticImageBuilders.BuildMachO([TextSegment()], uuid: UuidB, cpuType: 0x0100_0007);
            WriteDsym(dir.FullName, "app", SyntheticImageBuilders.BuildFat(other, matching));

            var result = NativeSymbolReader.Read(path, File.ReadAllBytes(path), []);

            Assert.AreEqual(NativeSymbolStatus.Loaded, result.Status);
            Assert.AreEqual(NativeSymbolSource.Dsym, result.Source);
            var symbol = Assert.ContainsSingle(result.Symbols);
            Assert.AreEqual("frost_main", symbol.Name);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a fat dSYM with no slice carrying the image's UUID is rejected as
    /// <see cref="NativeSymbolStatus.IdMismatch"/>, not silently read.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FatDsym_NoMatchingSlice_ReportsIdMismatch()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-fatdsym-");
        try
        {
            var path = Path.Combine(dir.FullName, "app");
            File.WriteAllBytes(path, SyntheticImageBuilders.BuildMachO(
                [TextSegment()],
                uuid: UuidA,
                functionStarts: new DwarfBlob().ULeb(0x1010).ULeb(0).ToArray()));

            var sliceB = SyntheticImageBuilders.BuildMachO([TextSegment()], uuid: UuidB);
            var sliceC = SyntheticImageBuilders.BuildMachO([TextSegment()], uuid: UuidC, cpuType: 0x0100_0007);
            WriteDsym(dir.FullName, "app", SyntheticImageBuilders.BuildFat(sliceB, sliceC));

            var result = NativeSymbolReader.Read(path, File.ReadAllBytes(path), []);

            Assert.AreEqual(NativeSymbolStatus.IdMismatch, result.Status);
            Assert.AreEqual(NativeSymbolSource.FunctionStartsFallback, result.Source);
            Assert.Contains("UUID", result.Diagnostic!);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies the real macOS fixture: the dSYM loads as the merged source, demangles to
    /// managed names, attributes a function to a source file and line, and carries data
    /// categories.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_NativeAotExeWithDsym_UsesDsymSource()
    {
        TestSkip.When(Samples.NativeAotConsoleDsym is null, "dSYM bundle not present on this platform");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var info = NativeSymbolReader.Read(
            Samples.NativeAotConsoleExe!, analyzer.RawBytes.ToArray(), analyzer.RecoveredTypes);

        Assert.AreEqual(NativeSymbolSource.Dsym, info.Source);
        Assert.AreEqual(NativeSymbolStatus.Loaded, info.Status);
        Assert.IsGreaterThan(1000, info.Symbols.Count, $"expected a real symbol population, got {info.Symbols.Count}");
        Assert.Contains(s => s.ManagedName is not null && s.IsExactMatch, info.Symbols);
        Assert.Contains(s => s.Kind == NativeSymbolKind.Function && s.SourceFile is not null && s.Line > 0, info.Symbols);
        Assert.Contains(s => s.Kind is NativeSymbolKind.MethodTable
            or NativeSymbolKind.Statics or NativeSymbolKind.FrozenObject, info.Symbols);
    }

    /// <summary>
    /// Verifies the deterministic symbol-free path on the real exe: with the copy's
    /// <c>LC_SYMTAB</c> count zeroed and no dSYM beside it, <c>LC_FUNCTION_STARTS</c> is forced
    /// regardless of what <c>strip -x</c> left in the symbol table.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_NativeAotExeSymtabZeroed_ForcesFunctionStarts()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        var bytes = File.ReadAllBytes(Samples.NativeAotConsoleExe!);
        TestSkip.When(!MachOImageReader.IsMachO(bytes), "exe is not a thin Mach-O on this platform");

        // Zero LC_SYMTAB's nsyms in place.
        var commandCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16));
        var command = 32;
        for (var i = 0; i < commandCount; i++)
        {
            var cmd = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(command));
            var cmdSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(command + 4));
            if (cmd == 0x2)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(command + 12), 0);
                break;
            }

            command += cmdSize;
        }

        var dir = Directory.CreateTempSubdirectory("dotsider-fstarts-");
        try
        {
            var copy = Path.Combine(dir.FullName, Path.GetFileName(Samples.NativeAotConsoleExe!));
            File.WriteAllBytes(copy, bytes);

            var info = NativeSymbolReader.Read(copy, File.ReadAllBytes(copy), []);

            Assert.AreEqual(NativeSymbolSource.FunctionStartsFallback, info.Source);
            Assert.AreEqual(NativeSymbolStatus.FallbackOnly, info.Status);
            Assert.IsNotEmpty(info.Symbols);
            TestAssert.All(info.Symbols, s => Assert.AreEqual(NativeSymbolKind.Boundary, s.Kind));
            TestAssert.All(info.Symbols, s => Assert.IsGreaterThan(0, s.Size));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
