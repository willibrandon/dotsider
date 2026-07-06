using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves a size-comparison input to its mstat report. A bare <c>.mstat</c> file is read
/// directly (detected by extension or by <see cref="MstatReader.Probe(string)"/> — an mstat is
/// itself a valid ECMA-335 assembly, so probing must come before any managed-assembly
/// interpretation); a Native AOT binary resolves through its sidecar discovery
/// (<c>app.mstat</c> beside the binary, or the ILC intermediate output tree). Anything else —
/// a managed assembly, a native binary without a size report — resolves to null.
/// </summary>
public static class MstatLocator
{
    /// <summary>
    /// Resolves a file to its mstat report, or null when the file is not mstat-backed.
    /// </summary>
    /// <param name="filePath">A <c>.mstat</c> file or a Native AOT binary.</param>
    /// <returns>The resolved source, or null.</returns>
    public static MstatSource? Resolve(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var isMstatByExtension = filePath.EndsWith(".mstat", StringComparison.OrdinalIgnoreCase);
        if (isMstatByExtension || MstatReader.Probe(filePath))
        {
            if (MstatReader.Read(filePath) is not { } data) return null;
            return new MstatSource(data, filePath, null, null, FindDgmlBeside(filePath));
        }

        try
        {
            using var analyzer = new AssemblyAnalyzer(filePath);
            if (analyzer.BinaryKind != BinaryKind.NativeAot) return null;
            if (analyzer.MstatPath is not { } mstatPath || analyzer.Mstat is not { } mstat) return null;

            return new MstatSource(
                mstat, mstatPath, filePath, new FileInfo(filePath).Length, analyzer.DgmlPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Probes for a DGML graph beside a bare <c>.mstat</c> — ILC writes both to the same
    /// intermediate directory, so <c>app.mstat</c> usually sits next to
    /// <c>app.codegen.dgml.xml</c>.
    /// </summary>
    private static string? FindDgmlBeside(string mstatPath)
    {
        var stem = mstatPath.EndsWith(".mstat", StringComparison.OrdinalIgnoreCase)
            ? mstatPath[..^".mstat".Length]
            : mstatPath;

        var codegen = stem + ".codegen.dgml.xml";
        if (File.Exists(codegen)) return codegen;

        var scan = stem + ".scan.dgml.xml";
        return File.Exists(scan) ? scan : null;
    }
}
