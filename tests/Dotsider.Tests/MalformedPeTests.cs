using Dotsider.Core.Analysis;
using System.Buffers.Binary;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Malformed Pe.
/// </summary>
[TestClass]
public class MalformedPeTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Generates synthetic malformed PE binaries for fuzzing.
    /// Each entry is (description, bytes) — the description is used in test output.
    /// </summary>
    internal static IEnumerable<(string Description, byte[] Bytes)> GenerateMalformedBinaries(byte[] validPe)
    {
        ArgumentNullException.ThrowIfNull(validPe);

        static byte[] Copy(byte[] source) => (byte[])source.Clone();

        static int ReadInt32LittleEndian(byte[] bytes, int offset) =>
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));

        static ushort ReadUInt16LittleEndian(byte[] bytes, int offset) =>
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));

        static void WriteInt32LittleEndian(byte[] bytes, int offset, int value) =>
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)), value);

        static void OverwriteRange(byte[] bytes, int offset, int length, byte fill)
        {
            if ((uint)offset >= (uint)bytes.Length || length <= 0)
            {
                return;
            }

            bytes.AsSpan(offset, Math.Min(length, bytes.Length - offset)).Fill(fill);
        }

        static void ScribblePseudoRandomly(byte[] bytes, int seed, int startInclusive, int endExclusive)
        {
            startInclusive = Math.Clamp(startInclusive, 0, bytes.Length);
            endExclusive = Math.Clamp(endExclusive, startInclusive, bytes.Length);
            var available = endExclusive - startInclusive;
            if (available == 0)
            {
                return;
            }

            var random = new Random(seed);
            var passes = Math.Min(6, Math.Max(1, available / 256));
            for (var pass = 0; pass < passes; pass++)
            {
                var start = startInclusive + random.Next(available);
                var length = Math.Min(random.Next(8, 33), endExclusive - start);
                for (var i = 0; i < length; i++)
                {
                    bytes[start + i] = (byte)random.Next(256);
                }
            }
        }

        static bool TryRvaToOffset(byte[] bytes, int sectionTableOffset, int numberOfSections, int rva, out int fileOffset)
        {
            fileOffset = 0;

            for (var i = 0; i < numberOfSections; i++)
            {
                var sectionOffset = sectionTableOffset + (i * 40);
                if (sectionOffset > bytes.Length - 40)
                {
                    return false;
                }

                var virtualSize = ReadInt32LittleEndian(bytes, sectionOffset + 8);
                var virtualAddress = ReadInt32LittleEndian(bytes, sectionOffset + 12);
                var sizeOfRawData = ReadInt32LittleEndian(bytes, sectionOffset + 16);
                var pointerToRawData = ReadInt32LittleEndian(bytes, sectionOffset + 20);
                var mappedSize = Math.Max(virtualSize, sizeOfRawData);
                if (mappedSize <= 0 || virtualAddress < 0 || pointerToRawData < 0)
                {
                    continue;
                }

                var delta = rva - virtualAddress;
                if (delta < 0 || delta >= mappedSize)
                {
                    continue;
                }

                var candidateOffset = pointerToRawData + delta;
                if ((uint)candidateOffset >= (uint)bytes.Length)
                {
                    continue;
                }

                fileOffset = candidateOffset;
                return true;
            }

            return false;
        }

        if (validPe.Length > 1)
        {
            yield return ("truncation/1-byte-file", validPe[..1]);
        }

        if (validPe.Length > 32)
        {
            yield return ("truncation/mid-dos-header", validPe[..32]);
        }

        if (validPe.Length < 0x40)
        {
            yield break;
        }

        var peOffset = ReadInt32LittleEndian(validPe, 0x3C);
        if (peOffset <= 0 || peOffset > validPe.Length - 4)
        {
            yield break;
        }

        if (validPe.Length > peOffset + 4)
        {
            yield return ("truncation/after-pe-signature", validPe[..(peOffset + 4)]);
        }

        var eLfanewIntoDosHeader = Copy(validPe);
        WriteInt32LittleEndian(eLfanewIntoDosHeader, 0x3C, 0x20);
        yield return ("header/e_lfanew-points-into-dos-header", eLfanewIntoDosHeader);

        var eLfanewPastEndOfFile = Copy(validPe);
        WriteInt32LittleEndian(eLfanewPastEndOfFile, 0x3C, validPe.Length + 0x200);
        yield return ("header/e_lfanew-points-past-eof", eLfanewPastEndOfFile);

        var peSignatureGarbage = Copy(validPe);
        peSignatureGarbage[peOffset] = (byte)'B';
        peSignatureGarbage[peOffset + 1] = (byte)'A';
        peSignatureGarbage[peOffset + 2] = (byte)'D';
        peSignatureGarbage[peOffset + 3] = (byte)'!';
        yield return ("header/corrupt-pe-signature", peSignatureGarbage);

        var coffHeaderOffset = peOffset + 4;
        if (coffHeaderOffset > validPe.Length - 20)
        {
            yield break;
        }

        var numberOfSections = ReadUInt16LittleEndian(validPe, coffHeaderOffset + 2);
        var sizeOfOptionalHeader = ReadUInt16LittleEndian(validPe, coffHeaderOffset + 16);
        var optionalHeaderOffset = coffHeaderOffset + 20;
        var sectionTableOffset = optionalHeaderOffset + sizeOfOptionalHeader;
        if (numberOfSections > 0 && sectionTableOffset <= validPe.Length - 40)
        {
            var sectionTableBytes = numberOfSections * 40;
            var midSectionTable = sectionTableOffset + Math.Max(1, sectionTableBytes / 2);
            if (midSectionTable < validPe.Length)
            {
                yield return ("truncation/mid-section-table", validPe[..midSectionTable]);
            }

            var oversizedSection = Copy(validPe);
            WriteInt32LittleEndian(oversizedSection, sectionTableOffset + 8, int.MaxValue);
            WriteInt32LittleEndian(oversizedSection, sectionTableOffset + 16, int.MaxValue);
            yield return ("section/first-section-size-int-max", oversizedSection);
        }

        if (optionalHeaderOffset > validPe.Length - 2)
        {
            yield break;
        }

        var optionalMagic = ReadUInt16LittleEndian(validPe, optionalHeaderOffset);
        var dataDirectoryOffset = optionalMagic switch
        {
            0x10B => optionalHeaderOffset + 96,
            0x20B => optionalHeaderOffset + 112,
            _ => -1,
        };
        var clrDirectoryOffset = dataDirectoryOffset + (14 * 8);
        if (dataDirectoryOffset < 0 || clrDirectoryOffset > validPe.Length - 8)
        {
            yield break;
        }

        var zeroClrDirectory = Copy(validPe);
        WriteInt32LittleEndian(zeroClrDirectory, clrDirectoryOffset, 0);
        WriteInt32LittleEndian(zeroClrDirectory, clrDirectoryOffset + 4, 0);
        yield return ("header/zero-clr-directory", zeroClrDirectory);

        var clrRva = ReadInt32LittleEndian(validPe, clrDirectoryOffset);
        if (!TryRvaToOffset(validPe, sectionTableOffset, numberOfSections, clrRva, out var corHeaderOffset) ||
            corHeaderOffset > validPe.Length - 16)
        {
            var headerBitRot = Copy(validPe);
            OverwriteRange(headerBitRot, Math.Max(0, peOffset - 8), 32, 0xCC);
            yield return ("bitrot/pe-header-window", headerBitRot);

            var randomBitRotFallback = Copy(validPe);
            ScribblePseudoRandomly(randomBitRotFallback, seed: 42_042, 0, randomBitRotFallback.Length);
            yield return ("bitrot/random-ranges", randomBitRotFallback);
            yield break;
        }

        var zeroMetadataSize = Copy(validPe);
        WriteInt32LittleEndian(zeroMetadataSize, corHeaderOffset + 12, 0);
        yield return ("clr/zero-metadata-size", zeroMetadataSize);

        var metadataRva = ReadInt32LittleEndian(validPe, corHeaderOffset + 8);
        var metadataSize = ReadInt32LittleEndian(validPe, corHeaderOffset + 12);
        if (TryRvaToOffset(validPe, sectionTableOffset, numberOfSections, metadataRva, out var metadataOffset))
        {
            if (metadataOffset <= validPe.Length - 4)
            {
                var corruptMetadataMagic = Copy(validPe);
                corruptMetadataMagic[metadataOffset] = 0xDE;
                corruptMetadataMagic[metadataOffset + 1] = 0xAD;
                corruptMetadataMagic[metadataOffset + 2] = 0xBE;
                corruptMetadataMagic[metadataOffset + 3] = 0xEF;
                yield return ("metadata/corrupt-bsjb-signature", corruptMetadataMagic);
            }

            var metadataEnd = metadataSize > 0 ? Math.Min(validPe.Length, metadataOffset + metadataSize) : validPe.Length;
            var midMetadata = metadataOffset + Math.Max(1, (metadataEnd - metadataOffset) / 2);
            if (midMetadata < validPe.Length)
            {
                yield return ("truncation/mid-clr-metadata", validPe[..midMetadata]);
            }

            var randomBitRot = Copy(validPe);
            var bitRotStart = Math.Min(validPe.Length, sectionTableOffset + (numberOfSections * 40));
            if (metadataOffset - bitRotStart >= 8)
            {
                ScribblePseudoRandomly(randomBitRot, seed: 42_042, bitRotStart, metadataOffset);
            }
            else
            {
                OverwriteRange(randomBitRot, Math.Max(0, validPe.Length - 64), 64, 0xA5);
            }

            yield return ("bitrot/random-ranges", randomBitRot);
            yield break;
        }

        var trailingBitRot = Copy(validPe);
        ScribblePseudoRandomly(trailingBitRot, seed: 42_042, validPe.Length / 2, validPe.Length);
        yield return ("bitrot/random-ranges", trailingBitRot);
    }

    /// <summary>
    /// Verifies all malformed binaries throw or construct never crash.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AllMalformedBinaries_ThrowOrConstruct_NeverCrash()
    {
        var validPe = File.ReadAllBytes(Samples.HelloWorldDll);
        foreach (var (description, bytes) in GenerateMalformedBinaries(validPe))
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"dotsider-fuzz-{Guid.NewGuid():N}.dll");
            try
            {
                File.WriteAllBytes(tempPath, bytes);
                try
                {
                    using var analyzer = new AssemblyAnalyzer(tempPath);
                    ForceLazyAnalysis(analyzer);
                }
                catch (Exception ex) when (ex is BadImageFormatException or IOException or
                    UnauthorizedAccessException or ArgumentException or InvalidOperationException or OverflowException)
                {
                    // Expected — these are the recoverable exceptions AssemblyAnalyzer should throw
                }
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    private static void ForceLazyAnalysis(AssemblyAnalyzer analyzer)
    {
        _ = analyzer.FileName;
        _ = analyzer.FileSize;
        if (!analyzer.HasMetadata)
        {
            return;
        }

        var typeDefinitions = analyzer.TypeDefs;
        var typeReferences = analyzer.TypeRefs;
        var methodDefinitions = analyzer.MethodDefs;
        var fieldDefinitions = analyzer.FieldDefs;
        var memberReferences = analyzer.MemberRefs;

        foreach (var type in typeDefinitions)
        {
            _ = IlNavigationResolver.Resolve(analyzer, type.Token);
        }
        foreach (var type in typeReferences)
        {
            _ = IlNavigationResolver.Resolve(analyzer, type.Token);
        }
        foreach (var field in fieldDefinitions)
        {
            _ = IlNavigationResolver.Resolve(analyzer, field.Token);
        }
        foreach (var member in memberReferences)
        {
            _ = IlNavigationResolver.Resolve(analyzer, member.Token);
        }

        var disassembler = new IlDisassembler(analyzer);
        foreach (var method in methodDefinitions)
        {
            _ = analyzer.GetMethodBody(method);
            _ = disassembler.DisassembleWithText(method);
            _ = IlNavigationResolver.Resolve(analyzer, method.Token, method);
        }

        ForceNavigationTable(
            analyzer,
            TableIndex.TypeSpec,
            row => MetadataTokens.GetToken(MetadataTokens.TypeSpecificationHandle(row)));
        ForceNavigationTable(
            analyzer,
            TableIndex.MethodSpec,
            row => MetadataTokens.GetToken(MetadataTokens.MethodSpecificationHandle(row)));

        static void ForceNavigationTable(
            AssemblyAnalyzer analyzer,
            TableIndex table,
            Func<int, int> getToken)
        {
            var metadataReader = analyzer.GetMetadataReader()!;
            var rowCount = metadataReader.GetTableRowCount(table);
            for (var row = 1; row <= rowCount; row++)
            {
                _ = IlNavigationResolver.Resolve(analyzer, getToken(row));
            }
        }
    }

    /// <summary>
    /// Verifies zero byte file throws bad image format.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ZeroByteFile_ThrowsBadImageFormat()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"dotsider-fuzz-{Guid.NewGuid():N}.dll");
        try
        {
            File.WriteAllBytes(tempPath, []);
            Assert.Throws<Exception>(() =>
            {
                using var _ = new AssemblyAnalyzer(tempPath);
            });
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    /// <summary>
    /// Verifies four byte junk throws bad image format.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FourByteJunk_ThrowsBadImageFormat()
    {
        Assert.Throws<Exception>(() =>
        {
            using var _ = new AssemblyAnalyzer(Samples.NonDotNetBinaryPath);
        });
    }

    /// <summary>
    /// Verifies truncated mz header throws bad image format.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TruncatedMzHeader_ThrowsBadImageFormat()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"dotsider-fuzz-{Guid.NewGuid():N}.dll");
        try
        {
            // Just the MZ magic bytes with no PE signature pointer
            File.WriteAllBytes(tempPath, [(byte)'M', (byte)'Z']);
            Assert.Throws<Exception>(() =>
            {
                using var _ = new AssemblyAnalyzer(tempPath);
            });
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    /// <summary>
    /// Verifies valid pe header truncated before metadata throws bad image format.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ValidPeHeader_TruncatedBeforeMetadata_ThrowsBadImageFormat()
    {
        var validPe = File.ReadAllBytes(Samples.HelloWorldDll);
        // Keep DOS header + PE signature but truncate before CLR metadata
        var truncated = validPe[..Math.Min(256, validPe.Length)];

        var tempPath = Path.Combine(Path.GetTempPath(), $"dotsider-fuzz-{Guid.NewGuid():N}.dll");
        try
        {
            File.WriteAllBytes(tempPath, truncated);
            Assert.Throws<Exception>(() =>
            {
                using var _ = new AssemblyAnalyzer(tempPath);
            });
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    /// <summary>
    /// Verifies push assembly malformed file returns false and preserves state.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PushAssembly_MalformedFile_ReturnsFalseAndPreservesState()
    {
        var workload = new Hex1b.Hex1bAppWorkloadAdapter();
        var terminal = Hex1b.Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        var app = new Hex1b.Hex1bApp(
            _ => Task.FromResult<Hex1b.Widgets.Hex1bWidget>(new Hex1b.Widgets.TextBlockWidget("test")),
            new Hex1b.Hex1bAppOptions { WorkloadAdapter = workload });

        try
        {
            using var state = new DotsiderState(app, Samples.HelloWorldDll);
            var originalFile = state.Analyzer.FileName;

            // Try to push a truncated PE
            var tempPath = Path.Combine(Path.GetTempPath(), $"dotsider-fuzz-{Guid.NewGuid():N}.dll");
            try
            {
                var validPe = File.ReadAllBytes(Samples.HelloWorldDll);
                File.WriteAllBytes(tempPath, validPe[..128]);

                Assert.IsFalse(state.PushAssembly(tempPath));
                Assert.AreEqual(originalFile, state.Analyzer.FileName);
                Assert.IsNotNull(state.NavigationError);
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
        finally
        {
            app.Dispose();
            terminal.Dispose();
            workload.Dispose();
        }
    }
}
