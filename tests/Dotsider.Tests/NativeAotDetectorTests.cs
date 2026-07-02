using System.Buffers.Binary;
using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the Native AOT detector.
/// </summary>
[Collection("SampleAssemblies")]
public class NativeAotDetectorTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies detect on a Native AOT executable returns a validated header.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_NativeAotExe_ReturnsValidatedHeader()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var result = NativeAotDetector.Detect(File.ReadAllBytes(samples.NativeAotConsoleExe!));

        Assert.NotNull(result);
        Assert.True(result.HeaderOffset > 0);
        Assert.InRange(result.MajorVersion, (ushort)1, (ushort)100);
        Assert.InRange(result.SectionCount, 1, 1000);
        Assert.InRange(result.EntrySize, (byte)8, (byte)64);
    }

    /// <summary>
    /// Verifies detect on a Native AOT executable recovers the runtime version.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_NativeAotExe_FindsRuntimeVersion()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var result = NativeAotDetector.Detect(File.ReadAllBytes(samples.NativeAotConsoleExe!));

        Assert.NotNull(result);
        Assert.NotNull(result.RuntimeVersion);
        Assert.Matches(@"^\d+\.\d+\.\d+", result.RuntimeVersion);
    }

    /// <summary>
    /// Verifies detect on a managed assembly returns null.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_ManagedDll_ReturnsNull()
    {
        var result = NativeAotDetector.Detect(File.ReadAllBytes(samples.RichLibraryDll));

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies detect on a native apphost executable returns null.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_ApphostExe_ReturnsNull()
    {
        var result = NativeAotDetector.Detect(File.ReadAllBytes(samples.HelloWorldExe));

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies a signature match with implausible header fields is rejected.
    /// The field values replicate a real code-immediate collision observed in
    /// ILC-generated machine code.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_FakeRtrSignatureFailsValidation_ReturnsNull()
    {
        var bytes = new byte[512];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        WriteHeader(bytes, offset: 64, majorVersion: 35649, minorVersion: 18937,
            sectionCount: 18650, entrySize: 139, entryType: 233, firstSectionId: 15041807);

        Assert.Null(NativeAotDetector.Detect(bytes));
    }

    /// <summary>
    /// Verifies a well-formed synthetic header is accepted.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_SyntheticValidHeader_ReturnsInfo()
    {
        var bytes = new byte[4096];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        WriteHeader(bytes, offset: 128, majorVersion: 16, minorVersion: 0,
            sectionCount: 33, entrySize: 24, entryType: 1, firstSectionId: 201);

        var result = NativeAotDetector.Detect(bytes);

        Assert.NotNull(result);
        Assert.Equal(128, result.HeaderOffset);
        Assert.Equal(16, result.MajorVersion);
        Assert.Equal(33, result.SectionCount);
        Assert.Equal(24, result.EntrySize);
        Assert.Null(result.RuntimeVersion);
    }

    /// <summary>
    /// Verifies a header whose section entries would run past the end of the
    /// file is rejected.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_TruncatedHeader_ReturnsNull()
    {
        var bytes = new byte[160];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';

        // 33 entries of 24 bytes need 792 bytes past the header; only 16 exist.
        WriteHeader(bytes, offset: 128, majorVersion: 16, minorVersion: 0,
            sectionCount: 33, entrySize: 24, entryType: 1, firstSectionId: 201);

        Assert.Null(NativeAotDetector.Detect(bytes));
    }

    /// <summary>
    /// Verifies bytes without a PE, ELF, or Mach-O magic are rejected even when
    /// they contain a well-formed header.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_NoExecutableMagic_ReturnsNull()
    {
        var bytes = new byte[4096];
        WriteHeader(bytes, offset: 128, majorVersion: 16, minorVersion: 0,
            sectionCount: 33, entrySize: 24, entryType: 1, firstSectionId: 201);

        Assert.Null(NativeAotDetector.Detect(bytes));
    }

    /// <summary>
    /// Verifies the scan skips an invalid candidate and accepts a later valid
    /// header, mirroring real binaries where a code immediate precedes the
    /// genuine header.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_FalsePositiveBeforeRealHeader_FindsRealHeader()
    {
        var bytes = new byte[8192];
        bytes[0] = (byte)'M';
        bytes[1] = (byte)'Z';
        WriteHeader(bytes, offset: 256, majorVersion: 35649, minorVersion: 18937,
            sectionCount: 18650, entrySize: 139, entryType: 233, firstSectionId: 15041807);
        WriteHeader(bytes, offset: 2048, majorVersion: 16, minorVersion: 0,
            sectionCount: 33, entrySize: 24, entryType: 1, firstSectionId: 201);

        var result = NativeAotDetector.Detect(bytes);

        Assert.NotNull(result);
        Assert.Equal(2048, result.HeaderOffset);
    }

    /// <summary>
    /// Verifies that corrupting the real header's version field in a copy of the
    /// real Native AOT binary makes detection fail — validation, not just the
    /// signature, is load-bearing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_NativeAotExe_CorruptedHeader_ReturnsNull()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var bytes = File.ReadAllBytes(samples.NativeAotConsoleExe!);
        var info = NativeAotDetector.Detect(bytes);
        Assert.NotNull(info);

        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(info.HeaderOffset + 4), 35649);

        Assert.Null(NativeAotDetector.Detect(bytes));
    }

    /// <summary>
    /// Verifies the real Native AOT binary truncated just past its header start
    /// is rejected because the section entries no longer fit.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_NativeAotExe_TruncatedAtHeader_ReturnsNull()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        var bytes = File.ReadAllBytes(samples.NativeAotConsoleExe!);
        var info = NativeAotDetector.Detect(bytes);
        Assert.NotNull(info);

        Assert.Null(NativeAotDetector.Detect(bytes.AsSpan(0, info.HeaderOffset + 20)));
    }

    /// <summary>
    /// Verifies a self-contained single-file bundle is not classified as Native AOT
    /// even though the ReadyToRun assemblies inside it contain RTR signatures.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_SelfContainedBundle_ReturnsNull()
    {
        Assert.SkipWhen(samples.SelfContainedConsoleExe is null,
            "Self-contained sample was not built");

        var result = NativeAotDetector.Detect(File.ReadAllBytes(samples.SelfContainedConsoleExe!));

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies the runtime-version heuristic finds a version placed after the
    /// anchor message and prefers the match nearest to it — the ELF layout, where
    /// the version lands a few hundred bytes past the anchor instead of
    /// immediately before it as in MSVC-linked PEs.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Detect_VersionAfterAnchor_NearestMatchWins()
    {
        var bytes = new byte[8192];
        bytes[0] = 0x7F;
        bytes[1] = (byte)'E';
        bytes[2] = (byte)'L';
        bytes[3] = (byte)'F';
        WriteHeader(bytes, offset: 512, majorVersion: 16, minorVersion: 0,
            sectionCount: 33, entrySize: 24, entryType: 1, firstSectionId: 201);

        var anchor = "Process is terminating due to StackOverflowException"u8;
        anchor.CopyTo(bytes.AsSpan(4096));
        "1.2.3"u8.CopyTo(bytes.AsSpan(4096 + anchor.Length + 700)); // decoy, farther away
        "10.0.5"u8.CopyTo(bytes.AsSpan(4096 + anchor.Length + 200));

        var result = NativeAotDetector.Detect(bytes);

        Assert.NotNull(result);
        Assert.Equal("10.0.5", result.RuntimeVersion);
    }

    /// <summary>
    /// Writes a ReadyToRun-shaped header into a buffer at the given offset.
    /// </summary>
    private static void WriteHeader(byte[] bytes, int offset, ushort majorVersion,
        ushort minorVersion, ushort sectionCount, byte entrySize, byte entryType,
        int firstSectionId)
    {
        var span = bytes.AsSpan(offset);
        "RTR\0"u8.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], majorVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(span[6..], minorVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..], sectionCount);
        span[14] = entrySize;
        span[15] = entryType;
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], firstSectionId);
    }
}
