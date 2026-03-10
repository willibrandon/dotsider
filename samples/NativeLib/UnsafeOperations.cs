namespace NativeLib;

/// <summary>
/// Unsafe code patterns that produce distinct IL opcodes (ldind, stind, conv, etc.).
/// </summary>
public static class UnsafeOperations
{
    /// <summary>
    /// Sum array elements using pointer arithmetic.
    /// IL: ldloc, conv.i, ldc.i4, mul, add, ldind.i4
    /// </summary>
    public static unsafe int SumWithPointers(int[] values)
    {
        var sum = 0;
        fixed (int* ptr = values)
        {
            for (var i = 0; i < values.Length; i++)
                sum += *(ptr + i);
        }
        return sum;
    }

    /// <summary>
    /// Swap two values via pointers.
    /// IL: ldind, stind
    /// </summary>
    public static unsafe void Swap(int* a, int* b)
    {
        (*a, *b) = (*b, *a);
    }

    /// <summary>
    /// Stack allocation with Span.
    /// IL: localloc
    /// </summary>
    public static int StackAllocSum(int count)
    {
        Span<int> buffer = stackalloc int[count];
        for (var i = 0; i < count; i++)
            buffer[i] = i + 1;

        var sum = 0;
        foreach (var val in buffer)
            sum += val;
        return sum;
    }
}

/// <summary>
/// Fixed-size buffer struct — produces distinct metadata in PE.
/// </summary>
public unsafe struct FixedBuffer
{
    /// <summary>
    /// Fixed-size byte buffer of 256 bytes.
    /// </summary>
    public fixed byte Data[256];

    /// <summary>
    /// The number of valid bytes in the buffer.
    /// </summary>
    public int Length;
}
