using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// The <c>RuntimeFunctions</c> section (102): a start-RVA-sorted table of precompiled code ranges.
/// Each record is 12 bytes on amd64 (<c>Start</c>, <c>End</c>, <c>Unwind</c> RVAs) and 8 bytes
/// elsewhere (<c>Start</c>, <c>Unwind</c>). A range's size is <c>End − Start</c> on amd64, and the
/// gap to the next record's start otherwise (the final record is bounded by its file segment) —
/// this avoids parsing per-architecture unwind info while keeping ranges correct.
/// </summary>
internal sealed class ReadyToRunRuntimeFunctionTable
{
    private readonly int[] _startRva;
    private readonly long[] _size;

    /// <summary>Parses the runtime-function records from the section's file offset.</summary>
    public ReadyToRunRuntimeFunctionTable(
        R2RNativeReader reader, int sectionFileOffset, int sectionSize,
        NativeArchitecture arch, ulong imageBase, NativeAddressSpace addressSpace)
    {
        var recordSize = arch == NativeArchitecture.X64 ? 12 : 8;
        var count = sectionSize / recordSize;
        _startRva = new int[count];
        var endRva = arch == NativeArchitecture.X64 ? new int[count] : null;

        for (var i = 0; i < count; i++)
        {
            var offset = sectionFileOffset + i * recordSize;
            var start = ApplyStartFixup(reader.ReadInt32(ref offset), arch);
            _startRva[i] = start;
            endRva?[i] = reader.ReadInt32(ref offset);
            // The unwind RVA (final dword) is not needed for range extents.
        }

        _size = new long[count];
        for (var i = 0; i < count; i++)
        {
            long size;
            if (endRva is not null)
                size = Math.Max(0, endRva[i] - _startRva[i]);
            else if (i + 1 < count)
                size = Math.Max(0, _startRva[i + 1] - _startRva[i]);
            else
                size = 0;

            if (addressSpace.TryGetFileOffset(imageBase + (uint)_startRva[i], out _, out var available))
                size = size == 0 ? available : Math.Min(size, available);
            _size[i] = size;
        }
    }

    /// <summary>The number of runtime functions.</summary>
    public int Count => _startRva.Length;

    /// <summary>The start RVA of runtime function <paramref name="index"/> (fixups applied).</summary>
    public int StartRva(int index) => _startRva[index];

    /// <summary>The byte size of runtime function <paramref name="index"/>.</summary>
    public long Size(int index) => _size[index];

    private static int ApplyStartFixup(int startRva, NativeArchitecture arch) => arch switch
    {
        // Thumb-2 functions carry the low bit set to mark thumb code; clear it for the real RVA.
        NativeArchitecture.Arm32 => startRva & ~1,
        // WASM stores the funclet flag in bit 31 and the virtual IP in bits 30:0.
        NativeArchitecture.Wasm32 => startRva & 0x7FFF_FFFF,
        _ => startRva,
    };
}
