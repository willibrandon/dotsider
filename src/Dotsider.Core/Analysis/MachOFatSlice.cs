namespace Dotsider.Core.Analysis;

/// <summary>
/// Describes one validated architecture slice in a Mach-O universal binary.
/// </summary>
/// <param name="CpuType">The slice's Mach CPU type.</param>
/// <param name="Offset">The slice's file offset in the universal binary.</param>
/// <param name="Size">The slice's byte size.</param>
internal readonly record struct MachOFatSlice(uint CpuType, long Offset, long Size);
