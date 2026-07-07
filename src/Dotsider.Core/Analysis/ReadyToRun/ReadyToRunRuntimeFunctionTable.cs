using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// The <c>RuntimeFunctions</c> section (102): a start-RVA-sorted table of precompiled code ranges.
/// Each record is 12 bytes on amd64 (<c>Start</c>, <c>End</c>, <c>Unwind</c> RVAs) and 8 bytes
/// elsewhere (<c>Start</c>, <c>Unwind</c>). A range's size is <c>End − Start</c> on amd64. On other
/// architectures, non-final ranges are bounded by the next runtime-function start; the final range
/// is bounded by architecture unwind length so data after the last body is not treated as code.
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
        var unwindRva = endRva is null ? new int[count] : null;

        for (var i = 0; i < count; i++)
        {
            var offset = sectionFileOffset + i * recordSize;
            var start = ApplyStartFixup(reader.ReadInt32(ref offset), arch);
            _startRva[i] = start;
            endRva?[i] = reader.ReadInt32(ref offset);
            unwindRva?[i] = reader.ReadInt32(ref offset);
        }

        _size = new long[count];
        for (var i = 0; i < count; i++)
        {
            long size;
            if (endRva is not null)
                size = Math.Max(0, endRva[i] - _startRva[i]);
            else if (i + 1 < count)
                size = Math.Max(0, _startRva[i + 1] - _startRva[i]);
            else if (TryReadUnwindLength(reader, imageBase, addressSpace, arch, unwindRva![i], out var unwindLength))
                size = unwindLength;
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

    private static bool TryReadUnwindLength(
        R2RNativeReader reader,
        ulong imageBase,
        NativeAddressSpace addressSpace,
        NativeArchitecture arch,
        int unwindRva,
        out long length)
    {
        length = 0;
        if (!addressSpace.TryGetFileOffset(imageBase + (uint)unwindRva, out var offset, out _))
            return false;

        try
        {
            switch (arch)
            {
                case NativeArchitecture.X86:
                    length = reader.DecodeUnsignedGc(ref offset);
                    return length > 0;

                case NativeArchitecture.Arm32:
                {
                    var header = reader.ReadInt32(ref offset);
                    length = (header & 0x3FFFF) * 2L;
                    return length > 0;
                }

                default:
                    return false;
            }
        }
        catch (BadImageFormatException)
        {
            length = 0;
            return false;
        }
    }
}
