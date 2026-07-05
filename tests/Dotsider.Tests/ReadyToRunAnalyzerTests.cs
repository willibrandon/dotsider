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
[Collection("SampleAssemblies")]
public class ReadyToRunAnalyzerTests(SampleAssemblyFixture samples)
{
    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    /// <summary>A non-composite R2R publish classifies as ReadyToRun, keeps metadata, and images its own code.</summary>
    [Fact(Timeout = 30_000)]
    public void NonComposite_ClassifiesAsReadyToRun()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);

        Assert.Equal(BinaryKind.ReadyToRun, analyzer.BinaryKind);
        Assert.True(analyzer.IsReadyToRun);
        Assert.True(analyzer.HasManagedMetadata, "an R2R image keeps its ECMA-335 metadata");
        Assert.True(analyzer.HasEmbeddedNativeCode);

        var info = analyzer.ReadyToRunInfo;
        Assert.NotNull(info);
        Assert.Equal(ReadyToRunStatus.Valid, info!.Status);
        Assert.False(info.IsComposite);
        Assert.False(info.IsComponent);

        // Architecture is precise, never Unknown.
        Assert.NotEqual(NativeArchitecture.Unknown, info.Architecture);

        // The code image for a non-composite image is itself.
        Assert.Same(analyzer, analyzer.ReadyToRunCodeImage);
    }

    /// <summary>Unix R2R images use CoreCLR's native-image machine encoding, not plain COFF values.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData(0xFD1D, NativeArchitecture.X64)] // AMD64 ^ Linux override
    [InlineData(0xC020, NativeArchitecture.X64)] // AMD64 ^ Apple override
    [InlineData(0xD11D, NativeArchitecture.Arm64)] // ARM64 ^ Linux override
    [InlineData(0xEC20, NativeArchitecture.Arm64)] // ARM64 ^ Apple override
    public void NativeImageMachineValues_MapToRealArchitecture(ushort machine, NativeArchitecture expected)
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        var bytes = PatchPeMachine(samples.ReadyToRunConsoleDll!, machine);

        using var analyzer = new AssemblyAnalyzer(bytes, samples.ReadyToRunConsoleDll!);

        Assert.Equal(BinaryKind.ReadyToRun, analyzer.BinaryKind);
        Assert.Equal(ReadyToRunStatus.Valid, analyzer.ReadyToRunInfo!.Status);
        Assert.Equal(expected, analyzer.ReadyToRunInfo.Architecture);
    }

    /// <summary>The real image's major version falls within the supported ceiling and reads as Valid.</summary>
    [Fact(Timeout = 30_000)]
    public void ValidImage_MajorVersion_IsWithinSupportedCeiling()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);
        var info = analyzer.ReadyToRunInfo;
        Assert.NotNull(info);

        // The acceptance rule: a major within the supported inspection window reads as Valid.
        Assert.InRange(
            info!.MajorVersion,
            ClassicReadyToRunDetector.MinimumInspectableMajorVersion,
            ClassicReadyToRunDetector.CurrentMajorVersion);
        Assert.Equal(ReadyToRunStatus.Valid, info.Status);
        Assert.Null(info.Diagnostic);
    }

    /// <summary>A valid image's native symbols come from the ReadyToRun source and load cleanly.</summary>
    [Fact(Timeout = 30_000)]
    public void ValidImage_NativeSymbols_LoadedFromReadyToRun()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);
        var symbols = analyzer.NativeSymbols;

        Assert.NotNull(symbols);
        Assert.Equal(NativeSymbolSource.ReadyToRun, symbols!.Source);
        Assert.Equal(NativeSymbolStatus.Loaded, symbols.Status);
        Assert.NotEmpty(symbols.Symbols);
    }

    /// <summary>The R2R apphost sits beside a managed companion that is itself the ReadyToRun image.</summary>
    [Fact(Timeout = 30_000)]
    public void Apphost_HasReadyToRunCompanionBeside()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleExe is null, SkipReason);
        // The apphost launcher sits beside its managed companion; the companion is the R2R image.
        var companion = Path.Combine(
            Path.GetDirectoryName(samples.ReadyToRunConsoleExe!)!, "ReadyToRunConsole.dll");
        Assert.True(File.Exists(companion), "the R2R companion must sit beside its apphost");

        using var analyzer = new AssemblyAnalyzer(companion);
        Assert.True(analyzer.IsReadyToRun);
        Assert.Equal(ReadyToRunStatus.Valid, analyzer.ReadyToRunInfo!.Status);
    }

    /// <summary>A plain managed library has no R2R header and stays classified as managed.</summary>
    [Fact(Timeout = 30_000)]
    public void PlainManaged_IsNotReadyToRun()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Null(analyzer.ReadyToRunInfo);
        Assert.False(analyzer.IsReadyToRun);
        Assert.Equal(BinaryKind.Managed, analyzer.BinaryKind);
    }

    // --- Malformed states no real crossgen2 publish can produce. Per the approved approach, these
    // patch the smallest bytes of a copy of the real fixture — the header is located through the
    // production parse (verified against the baseline major), never a hand-built image, never committed.

    /// <summary>A major below the inspection floor is surfaced as unsupported and parses no tables.</summary>
    [Fact(Timeout = 30_000)]
    public void MajorVersion_BelowFloor_IsUnsupportedAndParsesNothing()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        var bytes = PatchMajorVersion(samples.ReadyToRunConsoleDll!,
            (ushort)(ClassicReadyToRunDetector.MinimumInspectableMajorVersion - 1));

        using var analyzer = new AssemblyAnalyzer(bytes, samples.ReadyToRunConsoleDll!);
        var info = analyzer.ReadyToRunInfo;
        Assert.NotNull(info);
        Assert.Equal(ReadyToRunStatus.UnsupportedVersion, info!.Status);
        // Still surfaced as ReadyToRun with a diagnostic, never silently managed.
        Assert.Equal(BinaryKind.ReadyToRun, analyzer.BinaryKind);
        Assert.NotNull(info.Diagnostic);
        AssertNoTablesParsed(analyzer);
    }

    /// <summary>A major above the current version is surfaced as unsupported and parses no tables.</summary>
    [Fact(Timeout = 30_000)]
    public void MajorVersion_AboveCurrent_IsUnsupportedAndParsesNothing()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        var bytes = PatchMajorVersion(samples.ReadyToRunConsoleDll!,
            (ushort)(ClassicReadyToRunDetector.CurrentMajorVersion + 1));

        using var analyzer = new AssemblyAnalyzer(bytes, samples.ReadyToRunConsoleDll!);
        var info = analyzer.ReadyToRunInfo;
        Assert.NotNull(info);
        Assert.Equal(ReadyToRunStatus.UnsupportedVersion, info!.Status);
        Assert.Equal(BinaryKind.ReadyToRun, analyzer.BinaryKind);
        AssertNoTablesParsed(analyzer);
    }

    /// <summary>A section table claiming more rows than the file holds is corrupt and parses no tables.</summary>
    [Fact(Timeout = 30_000)]
    public void TruncatedSectionTable_IsCorruptAndParsesNothing()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        var (bytes, header) = LoadAndLocateHeader(samples.ReadyToRunConsoleDll!);
        // Section count is the u32 after signature(4) + major(2) + minor(2) + flags(4). Claim more rows
        // than the file can hold (bounded by the reader's 4096 guard) so the parse detects truncation.
        var maxRows = (bytes.Length - (header + 20)) / 12;
        var claimed = Math.Min(4096, maxRows + 64);
        BitConverter.GetBytes(claimed).CopyTo(bytes, header + 12);

        using var analyzer = new AssemblyAnalyzer(bytes, samples.ReadyToRunConsoleDll!);
        var info = analyzer.ReadyToRunInfo;
        Assert.NotNull(info);
        Assert.Equal(ReadyToRunStatus.Corrupt, info!.Status);
        Assert.Equal(BinaryKind.ReadyToRun, analyzer.BinaryKind);
        Assert.NotNull(info.Diagnostic);
        AssertNoTablesParsed(analyzer);
    }

    // A non-Valid image must expose header diagnostics only — no map, no loaded symbols, no correlation.
    private static void AssertNoTablesParsed(AssemblyAnalyzer analyzer)
    {
        Assert.Empty(analyzer.ReadyToRunMethods);
        Assert.Null(analyzer.ReadyToRunIndex);
        // Symbols carry a diagnostic status, never a misleading empty "loaded" set.
        Assert.NotEqual(NativeSymbolStatus.Loaded, analyzer.NativeSymbols?.Status);
        // Correlation refuses rather than reading a body out of an untrusted layout.
        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "get_Name", TestContext.Current.CancellationToken);
        Assert.Equal(ReadyToRunQueryOutcome.Unavailable, result.Outcome);
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
        Assert.NotNull(info);
        Assert.Equal(ReadyToRunStatus.Valid, info!.Status); // baseline parse proves the header is real

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
        Assert.True(bytes.Length >= 0x40, "fixture is too small for a PE header");
        var peOffset = BitConverter.ToInt32(bytes, 0x3C);
        Assert.InRange(peOffset, 0, bytes.Length - 6);
        Assert.Equal(0x0000_4550u, BitConverter.ToUInt32(bytes, peOffset));
        return peOffset + 4;
    }
}
