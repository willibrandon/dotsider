namespace Dotsider.Core.Analysis;

/// <summary>
/// Defines materialization limits for sections read from untrusted native images.
/// </summary>
internal static class NativeImageDataLimits
{
    /// <summary>
    /// The maximum compressed-stream overhead accepted beyond the remaining output budget.
    /// </summary>
    internal const int MaxCompressedOverheadBytes = 1024 * 1024;

    /// <summary>
    /// The maximum accepted ratio of declared output bytes to compressed zlib payload bytes.
    /// </summary>
    internal const int MaxCompressionRatio = 2_048;

    /// <summary>
    /// The maximum total section bytes materialized by one native-image analysis operation.
    /// </summary>
    internal const int MaxMaterializedBytes = 256 * 1024 * 1024;
}
