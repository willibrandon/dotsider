using Dotsider.Core.Analysis;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Builds JSON-ready Webcil payloads shared by CLI, MCP, and diagnostics session output.
/// Webcil is a managed assembly container used in browser-wasm publishes, so the payload is
/// provenance beside the normal metadata/IL facts rather than a separate native module view.
/// </summary>
public static class WebcilPayloadBuilder
{
    /// <summary>
    /// Builds a compact Webcil summary for protocol surfaces. Returns null when the analyzer did
    /// not open a Webcil assembly, allowing callers to include the property unconditionally.
    /// </summary>
    /// <param name="analyzer">The analyzer whose Webcil provenance should be serialized.</param>
    /// <returns>A JSON-ready Webcil summary object, or null when the analyzer is not Webcil.</returns>
    public static WebcilSummary? BuildSummary(AssemblyAnalyzer analyzer)
    {
        if (analyzer.WebcilInfo is not { } webcil)
            return null;

        return new WebcilSummary(
            webcil.VersionMajor,
            webcil.VersionMinor,
            webcil.IsWasmWrapped,
            webcil.PayloadOffset,
            webcil.SectionCount,
            webcil.MetadataSize,
            webcil.DebugDirectorySize);
    }
}
