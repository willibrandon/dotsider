using System.Reflection.PortableExecutable;

namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Display-ready PE debug directory entry information.
/// </summary>
/// <param name="Type">The debug directory entry type.</param>
/// <param name="Stamp">The entry stamp.</param>
/// <param name="MajorVersion">The major debug format version.</param>
/// <param name="MinorVersion">The minor debug format version.</param>
/// <param name="DataSize">The payload size in bytes.</param>
/// <param name="AddressOfRawData">The payload RVA.</param>
/// <param name="PointerToRawData">The payload file pointer.</param>
/// <param name="Payload">Inline payload summary for known entry types.</param>
public sealed record DebugDirectoryInfo(
    DebugDirectoryEntryType Type,
    uint Stamp,
    ushort MajorVersion,
    ushort MinorVersion,
    int DataSize,
    int AddressOfRawData,
    int PointerToRawData,
    string Payload);
