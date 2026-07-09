using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.ReadyToRun;

namespace Dotsider.Tests;

/// <summary>
/// Detection and classification for crossgen2 ReadyToRun images, asserted against the real published
/// fixtures: a non-composite R2R assembly classifies as <see cref="BinaryKind.ReadyToRun"/>, keeps
/// its ECMA-335 metadata, reports a precise architecture, and locates its code image in itself; a
/// plain managed library is untouched. The version-acceptance rule is validated on the real image's
/// actual major version.
/// </summary>
[TestClass]
public class ReadyToRunAnalyzerTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    /// <summary>A non-composite R2R publish classifies as ReadyToRun, keeps metadata, and images its own code.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NonComposite_ClassifiesAsReadyToRun()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);

        Assert.AreEqual(BinaryKind.ReadyToRun, analyzer.BinaryKind);
        Assert.IsTrue(analyzer.IsReadyToRun);
        Assert.IsTrue(analyzer.HasManagedMetadata, "an R2R image keeps its ECMA-335 metadata");
        Assert.IsTrue(analyzer.HasEmbeddedNativeCode);

        var info = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(info);
        Assert.AreEqual(ReadyToRunStatus.Valid, info!.Status);
        Assert.IsFalse(info.IsComposite);
        Assert.IsFalse(info.IsComponent);

        // Architecture is precise, never Unknown.
        Assert.AreNotEqual(NativeArchitecture.Unknown, info.Architecture);

        // The code image for a non-composite image is itself.
        Assert.AreSame(analyzer, analyzer.ReadyToRunCodeImage);
    }

    /// <summary>Unix R2R images use CoreCLR's native-image machine encoding, not plain COFF values.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow((ushort)0xFD1D, NativeArchitecture.X64)] // AMD64 ^ Linux override
    [DataRow((ushort)0xC020, NativeArchitecture.X64)] // AMD64 ^ Apple override
    [DataRow((ushort)0xD11D, NativeArchitecture.Arm64)] // ARM64 ^ Linux override
    [DataRow((ushort)0xEC20, NativeArchitecture.Arm64)] // ARM64 ^ Apple override
    public void NativeImageMachineValues_MapToRealArchitecture(ushort machine, NativeArchitecture expected)
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var bytes = PatchPeMachine(Samples.ReadyToRunConsoleDll!, machine);

        using var analyzer = new AssemblyAnalyzer(bytes, Samples.ReadyToRunConsoleDll!);

        Assert.AreEqual(BinaryKind.ReadyToRun, analyzer.BinaryKind);
        Assert.AreEqual(ReadyToRunStatus.Valid, analyzer.ReadyToRunInfo!.Status);
        Assert.AreEqual(expected, analyzer.ReadyToRunInfo.Architecture);
    }

    /// <summary>The real image's major version falls within the supported ceiling and reads as Valid.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ValidImage_MajorVersion_IsWithinSupportedCeiling()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);
        var info = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(info);

        // The acceptance rule: a major within the supported inspection window reads as Valid.
        Assert.IsInRange(ClassicReadyToRunDetector.MinimumInspectableMajorVersion, ClassicReadyToRunDetector.CurrentMajorVersion, info!.MajorVersion);
        Assert.AreEqual(ReadyToRunStatus.Valid, info.Status);
        Assert.IsNull(info.Diagnostic);
    }

    /// <summary>A valid image's native symbols come from the ReadyToRun source and load cleanly.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ValidImage_NativeSymbols_LoadedFromReadyToRun()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);
        var symbols = analyzer.NativeSymbols;

        Assert.IsNotNull(symbols);
        Assert.AreEqual(NativeSymbolSource.ReadyToRun, symbols!.Source);
        Assert.AreEqual(NativeSymbolStatus.Loaded, symbols.Status);
        Assert.IsNotEmpty(symbols.Symbols);
    }

    /// <summary>The R2R apphost sits beside a managed companion that is itself the ReadyToRun image.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Apphost_HasReadyToRunCompanionBeside()
    {
        TestSkip.When(Samples.ReadyToRunConsoleExe is null, SkipReason);
        // The apphost launcher sits beside its managed companion; the companion is the R2R image.
        var companion = Path.Combine(
            Path.GetDirectoryName(Samples.ReadyToRunConsoleExe!)!, "ReadyToRunConsole.dll");
        Assert.IsTrue(File.Exists(companion), "the R2R companion must sit beside its apphost");

        using var analyzer = new AssemblyAnalyzer(companion);
        Assert.IsTrue(analyzer.IsReadyToRun);
        Assert.AreEqual(ReadyToRunStatus.Valid, analyzer.ReadyToRunInfo!.Status);
    }

    /// <summary>A plain managed library has no R2R header and stays classified as managed.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PlainManaged_IsNotReadyToRun()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.IsNull(analyzer.ReadyToRunInfo);
        Assert.IsFalse(analyzer.IsReadyToRun);
        Assert.AreEqual(BinaryKind.Managed, analyzer.BinaryKind);
    }

    // --- Malformed states no real crossgen2 publish can produce. Per the approved approach, these
    // patch the smallest bytes of a copy of the real fixture — the header is located through the
    // production parse (verified against the baseline major), never a hand-built image, never committed.

    /// <summary>A major below the inspection floor is surfaced as unsupported and parses no tables.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MajorVersion_BelowFloor_IsUnsupportedAndParsesNothing()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var bytes = PatchMajorVersion(Samples.ReadyToRunConsoleDll!,
            (ushort)(ClassicReadyToRunDetector.MinimumInspectableMajorVersion - 1));

        using var analyzer = new AssemblyAnalyzer(bytes, Samples.ReadyToRunConsoleDll!);
        var info = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(info);
        Assert.AreEqual(ReadyToRunStatus.UnsupportedVersion, info!.Status);
        // Still surfaced as ReadyToRun with a diagnostic, never silently managed.
        Assert.AreEqual(BinaryKind.ReadyToRun, analyzer.BinaryKind);
        Assert.IsNotNull(info.Diagnostic);
        AssertNoTablesParsed(analyzer);
    }

    /// <summary>A major above the current version is surfaced as unsupported and parses no tables.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MajorVersion_AboveCurrent_IsUnsupportedAndParsesNothing()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var bytes = PatchMajorVersion(Samples.ReadyToRunConsoleDll!,
            (ushort)(ClassicReadyToRunDetector.CurrentMajorVersion + 1));

        using var analyzer = new AssemblyAnalyzer(bytes, Samples.ReadyToRunConsoleDll!);
        var info = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(info);
        Assert.AreEqual(ReadyToRunStatus.UnsupportedVersion, info!.Status);
        Assert.AreEqual(BinaryKind.ReadyToRun, analyzer.BinaryKind);
        AssertNoTablesParsed(analyzer);
    }

    /// <summary>A section table claiming more rows than the file holds is corrupt and parses no tables.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TruncatedSectionTable_IsCorruptAndParsesNothing()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var (bytes, header) = LoadAndLocateHeader(Samples.ReadyToRunConsoleDll!);
        // Section count is the u32 after signature(4) + major(2) + minor(2) + flags(4). Claim more rows
        // than the file can hold (bounded by the reader's 4096 guard) so the parse detects truncation.
        var maxRows = (bytes.Length - (header + 20)) / 12;
        var claimed = Math.Min(4096, maxRows + 64);
        BitConverter.GetBytes(claimed).CopyTo(bytes, header + 12);

        using var analyzer = new AssemblyAnalyzer(bytes, Samples.ReadyToRunConsoleDll!);
        var info = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(info);
        Assert.AreEqual(ReadyToRunStatus.Corrupt, info!.Status);
        Assert.AreEqual(BinaryKind.ReadyToRun, analyzer.BinaryKind);
        Assert.IsNotNull(info.Diagnostic);
        AssertNoTablesParsed(analyzer);
    }

    // A non-Valid image must expose header diagnostics only — no map, no loaded symbols, no correlation.
    private static void AssertNoTablesParsed(AssemblyAnalyzer analyzer)
    {
        Assert.IsEmpty(analyzer.ReadyToRunMethods);
        Assert.IsNull(analyzer.ReadyToRunIndex);
        // Symbols carry a diagnostic status, never a misleading empty "loaded" set.
        Assert.IsNotNull(analyzer.NativeSymbols);
        Assert.AreNotEqual(NativeSymbolStatus.Loaded, analyzer.NativeSymbols.Status);
        // Correlation refuses rather than reading a body out of an untrusted layout.
        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "get_Name", CancellationToken.None);
        Assert.AreEqual(ReadyToRunQueryOutcome.Unavailable, result.Outcome);
    }

    // The RTR signature dword is the ASCII bytes 'R','T','R','\0'.
    private static readonly byte[] RtrSignature = [0x52, 0x54, 0x52, 0x00];

    private static byte[] PatchMajorVersion(string path, ushort major)
    {
        var (bytes, header) = LoadAndLocateHeader(path);
        BitConverter.GetBytes(major).CopyTo(bytes, header + 4); // major u16 follows the 4-byte signature
        return bytes;
    }

    private static byte[] PatchPeMachine(string path, ushort machine)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = LocatePeMachineOffset(bytes);
        BitConverter.GetBytes(machine).CopyTo(bytes, offset);
        return bytes;
    }

    // Locates the R2R header in a copy of the real fixture through the production parse: the analyzer's
    // reported header + version proves where it is, and the signature at that spot confirms it.
    private static (byte[] Bytes, int HeaderOffset) LoadAndLocateHeader(string path)
    {
        var bytes = File.ReadAllBytes(path);
        using var analyzer = new AssemblyAnalyzer(path);
        var info = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(info);
        Assert.AreEqual(ReadyToRunStatus.Valid, info!.Status); // baseline parse proves the header is real

        for (var i = 0; i + 6 <= bytes.Length; i++)
        {
            if (bytes[i] == RtrSignature[0] && bytes[i + 1] == RtrSignature[1]
                && bytes[i + 2] == RtrSignature[2] && bytes[i + 3] == RtrSignature[3]
                && BitConverter.ToUInt16(bytes, i + 4) == info.MajorVersion)
            {
                return (bytes, i);
            }
        }

        Assert.Fail("could not locate the R2R header matching the parsed version");
        return (bytes, -1);
    }

    private static int LocatePeMachineOffset(byte[] bytes)
    {
        Assert.IsGreaterThanOrEqualTo(0x40, bytes.Length, "fixture is too small for a PE header");
        var peOffset = BitConverter.ToInt32(bytes, 0x3C);
        Assert.IsInRange(0, bytes.Length - 6, peOffset);
        Assert.AreEqual(0x0000_4550u, BitConverter.ToUInt32(bytes, peOffset));
        return peOffset + 4;
    }
}
