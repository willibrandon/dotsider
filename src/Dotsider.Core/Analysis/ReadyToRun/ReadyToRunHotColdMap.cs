namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// The <c>HotColdMap</c> section (120): pairs of <c>(cold runtime-function start, hot runtime-function id)</c>
/// that stitch a method's cold code back to its hot entry. Produces, per hot runtime-function id,
/// the contiguous block of cold runtime-function indices it owns, plus the index of the first cold
/// function (below which all functions are hot).
/// </summary>
internal sealed class ReadyToRunHotColdMap
{
    private readonly Dictionary<int, int[]> _hotToColdIndices;

    private ReadyToRunHotColdMap(Dictionary<int, int[]> hotToColdIndices, int firstColdRuntimeFunction)
    {
        _hotToColdIndices = hotToColdIndices;
        FirstColdRuntimeFunction = firstColdRuntimeFunction;
    }

    /// <summary>The lowest cold runtime-function index; every index below it is hot.</summary>
    public int FirstColdRuntimeFunction { get; }

    /// <summary>The cold runtime-function indices owned by the hot function at <paramref name="hotRuntimeFunctionId"/>, or null when it has none.</summary>
    public int[]? ColdIndicesFor(int hotRuntimeFunctionId) =>
        _hotToColdIndices.TryGetValue(hotRuntimeFunctionId, out var cold) ? cold : null;

    /// <summary>
    /// Reads the map, or returns an empty map (no cold code) when the section is absent. A method
    /// owns no runtime functions above <paramref name="totalRuntimeFunctions"/>.
    /// </summary>
    public static ReadyToRunHotColdMap Read(
        R2RNativeReader reader, int? sectionFileOffset, int sectionSize, int totalRuntimeFunctions)
    {
        var map = new Dictionary<int, int[]>();
        if (sectionFileOffset is not { } offset || sectionSize < 8)
            return new ReadyToRunHotColdMap(map, totalRuntimeFunctions);

        var count = sectionSize / 8;
        var pairs = new (int Cold, int Hot)[count];
        var cursor = offset;
        for (var i = 0; i < count; i++)
        {
            var cold = reader.ReadInt32(ref cursor);
            var hot = reader.ReadInt32(ref cursor);
            pairs[i] = (cold, hot);
        }

        for (var i = 0; i < count; i++)
        {
            var length = i + 1 < count ? pairs[i + 1].Cold - pairs[i].Cold : totalRuntimeFunctions - pairs[i].Cold;
            if (length <= 0) continue;
            var cold = new int[length];
            for (var j = 0; j < length; j++)
                cold[j] = pairs[i].Cold + j;
            map[pairs[i].Hot] = cold;
        }

        return new ReadyToRunHotColdMap(map, count > 0 ? pairs[0].Cold : totalRuntimeFunctions);
    }
}
