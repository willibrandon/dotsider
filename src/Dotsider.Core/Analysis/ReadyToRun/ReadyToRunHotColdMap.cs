namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// The <c>HotColdMap</c> section (120): pairs of <c>(cold runtime-function start, hot runtime-function id)</c>
/// that stitch a method's cold code back to its hot entry. The compact pair table is searched
/// directly so valid images do not allocate one array per method.
/// </summary>
internal sealed class ReadyToRunHotColdMap
{
    private readonly int[] _mapping;
    private readonly int _totalRuntimeFunctions;

    private ReadyToRunHotColdMap(int[] mapping, int totalRuntimeFunctions)
    {
        _mapping = mapping;
        _totalRuntimeFunctions = totalRuntimeFunctions;
        FirstColdRuntimeFunction = mapping.Length > 0 ? mapping[0] : totalRuntimeFunctions;
    }

    /// <summary>The lowest cold runtime-function index; every index below it is hot.</summary>
    public int FirstColdRuntimeFunction { get; }

    /// <summary>
    /// Validates and reads a complete hot/cold map.
    /// </summary>
    /// <param name="reader">The image reader.</param>
    /// <param name="addressSpace">The image's validated address space.</param>
    /// <param name="sectionFileOffset">The section's file offset, or null when the section is absent.</param>
    /// <param name="sectionSize">The section's byte size.</param>
    /// <param name="totalRuntimeFunctions">The runtime-function count that bounds every map index.</param>
    /// <param name="map">The parsed map when validation succeeds.</param>
    /// <param name="diagnostic">The validation diagnostic when parsing fails.</param>
    /// <returns>True when the section is absent or structurally valid and within its enclosing table.</returns>
    public static bool TryRead(
        R2RNativeReader reader,
        NativeAddressSpace addressSpace,
        int? sectionFileOffset,
        int sectionSize,
        int totalRuntimeFunctions,
        out ReadyToRunHotColdMap? map,
        out string? diagnostic)
    {
        map = null;
        diagnostic = null;

        if (totalRuntimeFunctions is < 0 or > ReadyToRunRuntimeFunctionTable.MaxRuntimeFunctionCount)
        {
            diagnostic = "ReadyToRun HotColdMap has an invalid runtime-function bound.";
            return false;
        }

        if (sectionSize < 0)
        {
            diagnostic = "ReadyToRun HotColdMap has a negative section size.";
            return false;
        }

        if (sectionSize == 0)
        {
            map = new ReadyToRunHotColdMap([], totalRuntimeFunctions);
            return true;
        }

        if (sectionFileOffset is not { } offset)
        {
            diagnostic = "ReadyToRun HotColdMap has no file-backed section range.";
            return false;
        }

        if (sectionSize % 8 != 0)
        {
            diagnostic = "ReadyToRun HotColdMap does not contain complete 8-byte pairs.";
            return false;
        }

        var entryCount = sectionSize / sizeof(int);
        if (entryCount > totalRuntimeFunctions)
        {
            diagnostic =
                $"ReadyToRun HotColdMap contains {entryCount} indices for {totalRuntimeFunctions} runtime functions.";
            return false;
        }

        if (!NativeImageRange.TryGet(reader.Length, offset, sectionSize, out var fileOffset, out var byteLength))
        {
            diagnostic = "ReadyToRun HotColdMap lies outside the image.";
            return false;
        }

        if (!addressSpace.TryGetAvailableBytes(fileOffset, out var available)
            || byteLength > available)
        {
            diagnostic = "ReadyToRun HotColdMap lies outside its file-backed image segment.";
            return false;
        }

        var sectionReader = reader.Slice(fileOffset, byteLength);
        var mapping = new int[entryCount];
        var cursor = fileOffset;
        var previousCold = -1;
        var previousHot = -1;
        var firstCold = -1;
        try
        {
            for (var i = 0; i < entryCount; i += 2)
            {
                var coldValue = sectionReader.ReadUInt32(ref cursor);
                var hotValue = sectionReader.ReadUInt32(ref cursor);
                if (coldValue >= (uint)totalRuntimeFunctions || hotValue >= (uint)totalRuntimeFunctions)
                {
                    diagnostic = "ReadyToRun HotColdMap contains an out-of-range runtime-function index.";
                    return false;
                }

                var cold = (int)coldValue;
                var hot = (int)hotValue;
                firstCold = firstCold < 0 ? cold : firstCold;
                if (cold <= previousCold || hot <= previousHot || hot >= firstCold)
                {
                    diagnostic = "ReadyToRun HotColdMap pairs are not in the required hot/cold order.";
                    return false;
                }

                mapping[i] = cold;
                mapping[i + 1] = hot;
                previousCold = cold;
                previousHot = hot;
            }
        }
        catch (BadImageFormatException)
        {
            diagnostic = "ReadyToRun HotColdMap is truncated.";
            return false;
        }

        map = new ReadyToRunHotColdMap(mapping, totalRuntimeFunctions);
        return true;
    }

    /// <summary>
    /// Finds the contiguous cold runtime-function range owned by a hot runtime-function.
    /// </summary>
    /// <param name="hotRuntimeFunctionId">The hot runtime-function index.</param>
    /// <param name="start">The first owned cold runtime-function index.</param>
    /// <param name="count">The number of owned cold runtime functions.</param>
    /// <returns>True when the hot runtime function owns a cold range.</returns>
    public bool TryGetColdRange(int hotRuntimeFunctionId, out int start, out int count)
    {
        var low = 0;
        var high = _mapping.Length / 2 - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var hot = _mapping[middle * 2 + 1];
            if (hot < hotRuntimeFunctionId)
            {
                low = middle + 1;
            }
            else if (hot > hotRuntimeFunctionId)
            {
                high = middle - 1;
            }
            else
            {
                start = _mapping[middle * 2];
                var end = middle + 1 < _mapping.Length / 2
                    ? _mapping[(middle + 1) * 2]
                    : _totalRuntimeFunctions;
                count = end - start;
                return true;
            }
        }

        start = 0;
        count = 0;
        return false;
    }
}
