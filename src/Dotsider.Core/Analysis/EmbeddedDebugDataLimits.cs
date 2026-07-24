namespace Dotsider.Core.Analysis;

/// <summary>
/// Defines materialization limits for embedded debug data read from untrusted images.
/// </summary>
internal static class EmbeddedDebugDataLimits
{
    internal const int MaxCompressedOverheadBytes = 1024 * 1024;
    internal const int MaxEmbeddedPortablePdbBytes = 256 * 1024 * 1024;
    internal const int MaxEmbeddedSourceBytes = 16 * 1024 * 1024;
}
