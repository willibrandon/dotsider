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
    /// <summary>
    /// The largest runtime-function table accepted from one ReadyToRun image.
    /// </summary>
    public const int MaxRuntimeFunctionCount = 1_048_576;

    private readonly long[] _size;
    private readonly int[] _startRva;

    private ReadyToRunRuntimeFunctionTable(int[] startRva, long[] size)
    {
        _startRva = startRva;
        _size = size;
    }

    /// <summary>The number of runtime functions.</summary>
    public int Count => _startRva.Length;

    /// <summary>
    /// Validates and reads a complete runtime-function table.
    /// </summary>
    /// <param name="reader">The image reader.</param>
    /// <param name="sectionFileOffset">The section's file offset.</param>
    /// <param name="sectionSize">The section's byte size.</param>
    /// <param name="arch">The image architecture.</param>
    /// <param name="imageBase">The image base used to map RVAs.</param>
    /// <param name="addressSpace">The image's validated address space.</param>
    /// <param name="table">The parsed table when validation succeeds.</param>
    /// <param name="diagnostic">The validation diagnostic when parsing fails.</param>
    /// <returns>True when the complete section is structurally valid and within the resource budget.</returns>
    public static bool TryRead(
        R2RNativeReader reader,
        int sectionFileOffset,
        int sectionSize,
        NativeArchitecture arch,
        ulong imageBase,
        NativeAddressSpace addressSpace,
        out ReadyToRunRuntimeFunctionTable? table,
        out string? diagnostic)
    {
        table = null;
        diagnostic = null;

        if (!TryGetRecordSize(arch, out var recordSize))
        {
            diagnostic = "ReadyToRun RuntimeFunctions uses an unsupported architecture.";
            return false;
        }

        if (sectionSize < 0)
        {
            diagnostic = "ReadyToRun RuntimeFunctions has a negative section size.";
            return false;
        }

        if (sectionSize % recordSize != 0)
        {
            diagnostic = $"ReadyToRun RuntimeFunctions size is not aligned to its {recordSize}-byte record layout.";
            return false;
        }

        var count = sectionSize / recordSize;
        if (count > MaxRuntimeFunctionCount)
        {
            diagnostic =
                $"ReadyToRun RuntimeFunctions contains {count} records; the limit is 1,048,576.";
            return false;
        }

        if (!NativeImageRange.TryGet(
                reader.Length,
                sectionFileOffset,
                sectionSize,
                out var fileOffset,
                out var byteLength))
        {
            diagnostic = "ReadyToRun RuntimeFunctions lies outside the image.";
            return false;
        }

        if (!addressSpace.TryGetAvailableBytes(fileOffset, out var available)
            || byteLength > available)
        {
            diagnostic = "ReadyToRun RuntimeFunctions lies outside its file-backed image segment.";
            return false;
        }

        var sectionReader = reader.Slice(fileOffset, byteLength);
        var startRva = new int[count];
        var size = new long[count];
        var cursor = fileOffset;

        try
        {
            if (arch == NativeArchitecture.X64)
            {
                uint previousStart = 0;
                for (var i = 0; i < count; i++)
                {
                    var start = ApplyStartFixup(sectionReader.ReadInt32(ref cursor), arch);
                    var end = sectionReader.ReadUInt32(ref cursor);
                    _ = sectionReader.ReadUInt32(ref cursor);

                    startRva[i] = start;
                    var startValue = unchecked((uint)start);
                    if ((i > 0 && startValue < previousStart) || end < startValue)
                    {
                        diagnostic = "ReadyToRun RuntimeFunctions contains an invalid RVA range order.";
                        return false;
                    }

                    var declaredSize = (long)end - startValue;
                    if (!TryClampSize(
                            imageBase,
                            addressSpace,
                            startValue,
                            declaredSize,
                            out size[i]))
                    {
                        diagnostic = "ReadyToRun RuntimeFunctions contains an overflowing virtual address.";
                        return false;
                    }

                    previousStart = startValue;
                }
            }
            else
            {
                uint previousStart = 0;
                for (var i = 0; i < count; i++)
                {
                    var start = ApplyStartFixup(sectionReader.ReadInt32(ref cursor), arch);
                    var unwindRva = sectionReader.ReadInt32(ref cursor);
                    var startValue = unchecked((uint)start);
                    startRva[i] = start;

                    if (i > 0)
                    {
                        if (startValue < previousStart)
                        {
                            diagnostic = "ReadyToRun RuntimeFunctions contains an invalid RVA range order.";
                            return false;
                        }

                        var declaredSize = (long)startValue - previousStart;
                        if (!TryClampSize(
                                imageBase,
                                addressSpace,
                                previousStart,
                                declaredSize,
                                out size[i - 1]))
                        {
                            diagnostic = "ReadyToRun RuntimeFunctions contains an overflowing virtual address.";
                            return false;
                        }
                    }

                    if (i == count - 1)
                    {
                        var declaredSize = TryReadUnwindLength(
                            sectionReader,
                            imageBase,
                            addressSpace,
                            arch,
                            unwindRva,
                            out var unwindLength)
                            ? unwindLength
                            : 0;
                        if (!TryClampSize(
                                imageBase,
                                addressSpace,
                                startValue,
                                declaredSize,
                                out size[i]))
                        {
                            diagnostic = "ReadyToRun RuntimeFunctions contains an overflowing virtual address.";
                            return false;
                        }
                    }

                    previousStart = startValue;
                }
            }
        }
        catch (BadImageFormatException)
        {
            diagnostic = "ReadyToRun RuntimeFunctions is truncated.";
            return false;
        }

        table = new ReadyToRunRuntimeFunctionTable(startRva, size);
        return true;
    }

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

    private static bool TryClampSize(
        ulong imageBase,
        NativeAddressSpace addressSpace,
        uint startRva,
        long declaredSize,
        out long size)
    {
        size = 0;
        if (!NativeImageRange.TryAdd(imageBase, startRva, out var virtualAddress))
        {
            return false;
        }

        if (!NativeImageRange.TryAdd(virtualAddress, (ulong)declaredSize, out _))
        {
            return false;
        }

        if (addressSpace.TryGetFileOffset(virtualAddress, out _, out var available))
        {
            size = Math.Min(declaredSize, available);
        }

        return true;
    }

    private static bool TryGetRecordSize(NativeArchitecture arch, out int recordSize)
    {
        recordSize = arch switch
        {
            NativeArchitecture.X64 => 12,
            NativeArchitecture.Arm32
                or NativeArchitecture.Arm64
                or NativeArchitecture.LoongArch64
                or NativeArchitecture.RiscV64
                or NativeArchitecture.Wasm32
                or NativeArchitecture.X86 => 8,
            _ => 0,
        };
        return recordSize != 0;
    }

    private static bool TryReadUnwindLength(
        R2RNativeReader reader,
        ulong imageBase,
        NativeAddressSpace addressSpace,
        NativeArchitecture arch,
        int unwindRva,
        out long length)
    {
        length = 0;
        if (!NativeImageRange.TryAdd(imageBase, unchecked((uint)unwindRva), out var unwindAddress)
            || !addressSpace.TryGetFileOffset(unwindAddress, out var offset, out _))
        {
            return false;
        }

        try
        {
            switch (arch)
            {
                case NativeArchitecture.X86:
                    length = reader.DecodeUnsignedGc(ref offset);
                    return length > 0;

                case NativeArchitecture.Arm32:
                    var header = reader.ReadInt32(ref offset);
                    length = (header & 0x3FFFF) * 2L;
                    return length > 0;

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
