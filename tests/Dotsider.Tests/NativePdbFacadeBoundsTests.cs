using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Tests;

/// <summary>Verifies malformed matching native PDBs degrade through the public symbol facade.</summary>
[TestClass]
public sealed class NativePdbFacadeBoundsTests
{
    /// <summary>
    /// Verifies malicious DBI ranges in a matching companion PDB report corruption and preserve
    /// contained `.pdata` fallback boundaries.
    /// </summary>
    [TestMethod]
    public void Read_MatchingPdbWithMaliciousDbi_UsesCorruptPdataFallback()
    {
        var guid = Guid.NewGuid();
        const int age = 7;
        const string pdbName = "malicious.pdb";
        var image = BuildPeWithCodeViewAndPdata(guid, age, pdbName);
        var pdb = SyntheticImageBuilders.BuildMsf(
            512,
            PdbInfoStream(guid, age),
            [],
            DbiStreamWithValidAndMaliciousModules(),
            ValidModuleStream(),
            new byte[64]);
        var directory = Directory.CreateTempSubdirectory("dotsider-native-pdb-bounds-");

        try
        {
            var imagePath = Path.Combine(directory.FullName, "fixture.exe");
            File.WriteAllBytes(imagePath, image);
            File.WriteAllBytes(Path.Combine(directory.FullName, pdbName), pdb);

            var info = NativeSymbolReader.Read(imagePath, image, []);

            Assert.AreEqual(NativeSymbolStatus.CorruptSymbolFile, info.Status);
            Assert.AreEqual(NativeSymbolSource.PdataFallback, info.Source);
            Assert.AreEqual(NativeArchitecture.X64, info.Architecture);
            Assert.Contains(pdbName, info.Diagnostic!);
            Assert.Contains(".pdata", info.Diagnostic!);
            var symbol = Assert.ContainsSingle(info.Symbols);
            Assert.AreEqual("sub_1100", symbol.Name);
            Assert.AreEqual(0x1100U, symbol.Rva);
            Assert.AreEqual(0x20L, symbol.Size);
            Assert.AreEqual(NativeSymbolKind.Boundary, symbol.Kind);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static byte[] BuildPeWithCodeViewAndPdata(Guid guid, int age, string pdbName)
    {
        const int sectionFileOffset = 0x400;
        const int pdataOffset = 0;
        const uint pdataRva = 0x1000;
        const int debugDirectoryOffset = 0x40;
        const uint debugDirectoryRva = 0x1040;
        const int codeViewOffset = 0x80;
        const uint codeViewRva = 0x1080;
        var section = new byte[0x200];

        BinaryPrimitives.WriteUInt32LittleEndian(section.AsSpan(pdataOffset), 0x1100);
        BinaryPrimitives.WriteUInt32LittleEndian(section.AsSpan(pdataOffset + 4), 0x1120);
        BinaryPrimitives.WriteUInt32LittleEndian(section.AsSpan(pdataOffset + 8), 0x1180);

        var pathBytes = Encoding.UTF8.GetBytes(pdbName + "\0");
        var codeViewSize = 24 + pathBytes.Length;
        var debug = section.AsSpan(debugDirectoryOffset, 28);
        BinaryPrimitives.WriteUInt32LittleEndian(debug[12..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(debug[16..], (uint)codeViewSize);
        BinaryPrimitives.WriteUInt32LittleEndian(debug[20..], codeViewRva);
        BinaryPrimitives.WriteUInt32LittleEndian(
            debug[24..],
            (uint)(sectionFileOffset + codeViewOffset));

        var codeView = section.AsSpan(codeViewOffset, codeViewSize);
        BinaryPrimitives.WriteUInt32LittleEndian(codeView, 0x5344_5352);
        guid.TryWriteBytes(codeView[4..20]);
        BinaryPrimitives.WriteInt32LittleEndian(codeView[20..], age);
        pathBytes.CopyTo(codeView[24..]);

        var image = SyntheticImageBuilders.BuildPe(
            0x8664,
            section,
            pdataRva,
            12);
        var peHeader = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(0x3C));
        var optionalHeader = peHeader + 24;
        var debugDataDirectory = optionalHeader + 112 + 6 * 8;
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(debugDataDirectory),
            debugDirectoryRva);
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(debugDataDirectory + 4),
            28);
        return image;
    }

    private static byte[] DbiStreamWithValidAndMaliciousModules()
    {
        const int moduleRecordSize = 68;
        var stream = new byte[64 + 2 * moduleRecordSize];
        BinaryPrimitives.WriteInt32LittleEndian(stream, -1);
        BinaryPrimitives.WriteUInt16LittleEndian(stream.AsSpan(16), ushort.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(stream.AsSpan(20), ushort.MaxValue);
        BinaryPrimitives.WriteInt32LittleEndian(
            stream.AsSpan(24),
            2 * moduleRecordSize);

        var validModuleOffset = 64;
        BinaryPrimitives.WriteUInt16LittleEndian(
            stream.AsSpan(validModuleOffset + 34),
            4);
        BinaryPrimitives.WriteUInt32LittleEndian(
            stream.AsSpan(validModuleOffset + 36),
            (uint)ValidModuleStream().Length);

        var maliciousModuleOffset = validModuleOffset + moduleRecordSize;
        BinaryPrimitives.WriteUInt16LittleEndian(
            stream.AsSpan(maliciousModuleOffset + 34),
            5);
        BinaryPrimitives.WriteUInt32LittleEndian(
            stream.AsSpan(maliciousModuleOffset + 36),
            uint.MaxValue);
        return stream;
    }

    private static byte[] ValidModuleStream()
    {
        var name = "ValidData\0"u8;
        var bodyLength = 10 + name.Length;
        var length = checked((ushort)(sizeof(ushort) + bodyLength));
        var stream = new byte[4 + sizeof(ushort) + length];
        var record = stream.AsSpan(4);
        BinaryPrimitives.WriteUInt16LittleEndian(record, length);
        BinaryPrimitives.WriteUInt16LittleEndian(record[2..], 0x110D);
        BinaryPrimitives.WriteUInt32LittleEndian(record[8..], 0x100);
        BinaryPrimitives.WriteUInt16LittleEndian(record[12..], 1);
        name.CopyTo(record[14..]);
        return stream;
    }

    private static byte[] PdbInfoStream(Guid guid, int age)
    {
        var stream = new byte[28];
        BinaryPrimitives.WriteInt32LittleEndian(stream, 20000404);
        BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(8), age);
        guid.TryWriteBytes(stream.AsSpan(12));
        return stream;
    }
}
