namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Facts extracted from the embedded ReadyToRun header of a Native AOT binary.
/// Every Native AOT image embeds this header (signature "RTR\0") so the runtime can
/// locate its module sections; its presence with no COR header identifies the binary
/// as Native AOT compiled .NET.
/// </summary>
/// <param name="HeaderOffset">File offset of the RTR signature.</param>
/// <param name="MajorVersion">ReadyToRun format major version — the ILC toolchain format version.</param>
/// <param name="MinorVersion">ReadyToRun format minor version.</param>
/// <param name="Flags">Raw header flags.</param>
/// <param name="SectionCount">Number of module section entries following the header.</param>
/// <param name="EntrySize">Size in bytes of each module section entry.</param>
/// <param name="RuntimeVersion">
/// Heuristically detected runtime version (e.g. "10.0.8"), or null when not found.
/// Recovered from a version string the runtime pack embeds near a well-known error
/// message; absence is normal for stripped or unusually linked binaries.
/// </param>
public sealed record NativeAotInfo(
    int HeaderOffset,
    ushort MajorVersion,
    ushort MinorVersion,
    uint Flags,
    int SectionCount,
    byte EntrySize,
    string? RuntimeVersion);
