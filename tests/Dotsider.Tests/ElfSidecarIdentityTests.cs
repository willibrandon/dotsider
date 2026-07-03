using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="ElfSidecarIdentity"/> and the note/debuglink readers behind it — the
/// all-present-signals-must-match rule that decides whether a <c>.dbg</c> sidecar belongs to a
/// stripped image.
/// </summary>
public class ElfSidecarIdentityTests
{
    private static readonly byte[] IdA = [0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04];
    private static readonly byte[] IdB = [0xCA, 0xFE, 0xBA, 0xBE, 0x05, 0x06, 0x07, 0x08];

    private static byte[] ImageWithBuildId(byte[] id) =>
        SyntheticImageBuilders.BuildElf(
            (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(id)));

    private static byte[] SidecarWithBuildId(byte[] id) =>
        SyntheticImageBuilders.BuildElf(
            (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(id)),
            (".debug_info", 0, new byte[] { 1 }));

    /// <summary>Verifies the standard CRC-32 check value.</summary>
    [Fact(Timeout = 30_000)]
    public void Crc32_KnownAnswer()
    {
        Assert.Equal(0xCBF43926U, Crc32.Compute("123456789"u8));
        Assert.Equal(0U, Crc32.Compute([]));
    }

    /// <summary>Verifies the build-id note parses, including walking past a foreign note first.</summary>
    [Fact(Timeout = 30_000)]
    public void TryReadBuildId_ParsesGnuNoteBehindForeignNote()
    {
        var image = SyntheticImageBuilders.BuildElf(
            (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(IdA, precedeWithForeignNote: true)));

        Assert.True(ElfImageReader.TryReadBuildId(image, out var id));
        Assert.Equal(IdA, id);

        var plain = SyntheticImageBuilders.BuildElf((".text", 0, new byte[] { 1 }));
        Assert.False(ElfImageReader.TryReadBuildId(plain, out _));

        var malformed = SyntheticImageBuilders.BuildElf((".note.gnu.build-id", 0, new byte[] { 1, 2, 3 }));
        Assert.False(ElfImageReader.TryReadBuildId(malformed, out _));
    }

    /// <summary>Verifies the debuglink reader returns the file name and the 4-aligned CRC.</summary>
    [Fact(Timeout = 30_000)]
    public void TryReadDebugLink_ParsesNameAndAlignedCrc()
    {
        // "myapp.dbg" + NUL = 10 bytes -> CRC 4-aligned at offset 12.
        var image = SyntheticImageBuilders.BuildElf(
            (".gnu_debuglink", 0, SyntheticImageBuilders.GnuDebugLink("myapp.dbg", 0x1234_5678)));

        Assert.True(ElfImageReader.TryReadDebugLink(image, out var name, out var crc));
        Assert.Equal("myapp.dbg", name);
        Assert.Equal(0x1234_5678U, crc);

        var absent = SyntheticImageBuilders.BuildElf((".text", 0, new byte[] { 1 }));
        Assert.False(ElfImageReader.TryReadDebugLink(absent, out _, out _));
    }

    /// <summary>Verifies matching build ids accept the sidecar.</summary>
    [Fact(Timeout = 30_000)]
    public void Check_BuildIdMatch_Accepts()
    {
        Assert.Equal(ElfSidecarMatch.Matched,
            ElfSidecarIdentity.Check(ImageWithBuildId(IdA), SidecarWithBuildId(IdA)));
    }

    /// <summary>Verifies a differing or absent sidecar build id rejects the sidecar.</summary>
    [Fact(Timeout = 30_000)]
    public void Check_BuildIdMismatchOrAbsent_Rejects()
    {
        Assert.Equal(ElfSidecarMatch.Mismatched,
            ElfSidecarIdentity.Check(ImageWithBuildId(IdA), SidecarWithBuildId(IdB)));

        var noIdSidecar = SyntheticImageBuilders.BuildElf((".debug_info", 0, new byte[] { 1 }));
        Assert.Equal(ElfSidecarMatch.Mismatched,
            ElfSidecarIdentity.Check(ImageWithBuildId(IdA), noIdSidecar));
    }

    /// <summary>Verifies the debuglink CRC alone can accept, and a CRC failure rejects.</summary>
    [Fact(Timeout = 30_000)]
    public void Check_DebugLinkCrc_AcceptsOnMatchRejectsOnFailure()
    {
        var sidecar = SyntheticImageBuilders.BuildElf((".debug_info", 0, new byte[] { 1 }));
        var crc = Crc32.Compute(sidecar);

        var matching = SyntheticImageBuilders.BuildElf(
            (".gnu_debuglink", 0, SyntheticImageBuilders.GnuDebugLink("app.dbg", crc)));
        Assert.Equal(ElfSidecarMatch.Matched, ElfSidecarIdentity.Check(matching, sidecar));

        var failing = SyntheticImageBuilders.BuildElf(
            (".gnu_debuglink", 0, SyntheticImageBuilders.GnuDebugLink("app.dbg", crc ^ 1)));
        Assert.Equal(ElfSidecarMatch.Mismatched, ElfSidecarIdentity.Check(failing, sidecar));
    }

    /// <summary>
    /// Verifies every present signal must pass: one failing signal rejects even when the other
    /// matches, in both directions.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Check_BothSignals_OneFailureRejects()
    {
        var sidecar = SidecarWithBuildId(IdA);
        var crc = Crc32.Compute(sidecar);

        byte[] Image(byte[] id, uint expectedCrc) => SyntheticImageBuilders.BuildElf(
            (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(id)),
            (".gnu_debuglink", 0, SyntheticImageBuilders.GnuDebugLink("app.dbg", expectedCrc)));

        Assert.Equal(ElfSidecarMatch.Matched, ElfSidecarIdentity.Check(Image(IdA, crc), sidecar));
        Assert.Equal(ElfSidecarMatch.Mismatched, ElfSidecarIdentity.Check(Image(IdA, crc ^ 1), sidecar));
        Assert.Equal(ElfSidecarMatch.Mismatched, ElfSidecarIdentity.Check(Image(IdB, crc), sidecar));
    }

    /// <summary>
    /// Verifies a signal-free image accepts only loosely — same machine and real debug info —
    /// and rejects when the machine differs or the sidecar lacks <c>.debug_info</c>.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Check_NoSignals_LooseChecksDecide()
    {
        var image = SyntheticImageBuilders.BuildElf((".text", 0, new byte[] { 1 }));
        var sidecar = SyntheticImageBuilders.BuildElf((".debug_info", 0, new byte[] { 1 }));

        Assert.Equal(ElfSidecarMatch.LooseMatch, ElfSidecarIdentity.Check(image, sidecar));

        var foreignMachine = SyntheticImageBuilders.BuildElf((".debug_info", 0, new byte[] { 1 }));
        foreignMachine[18] = 0x28; // EM_ARM
        Assert.Equal(ElfSidecarMatch.Mismatched, ElfSidecarIdentity.Check(image, foreignMachine));

        var noDebugInfo = SyntheticImageBuilders.BuildElf((".text", 0, new byte[] { 1 }));
        Assert.Equal(ElfSidecarMatch.Mismatched, ElfSidecarIdentity.Check(image, noDebugInfo));
    }
}
