using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves the total-size basis for a comparison of mstat inputs. The rule is shared by the
/// CLI, the MCP server, and the session protocol so a size figure never changes meaning
/// between surfaces: binaries measure file size on disk; a bare <c>.mstat</c> anywhere forces
/// mstat totals for both sides.
/// </summary>
public static class SizeBasisResolver
{
    /// <summary>
    /// Resolves the basis and totals for a target and optional baseline.
    /// </summary>
    /// <param name="target">The build under check.</param>
    /// <param name="baseline">The baseline, or null when the check runs without one.</param>
    /// <param name="diff">The computed size diff, whose summary carries the mstat totals.</param>
    /// <returns>The shared-basis totals.</returns>
    public static SizeTotals Resolve(MstatSource target, MstatSource? baseline, MstatDiffResult diff)
    {
        var useFileSize = target.BinaryFileSize is not null
            && (baseline is null || baseline.BinaryFileSize is not null);
        return new SizeTotals(
            useFileSize ? SizeBasis.FileSize : SizeBasis.MstatTotal,
            useFileSize ? target.BinaryFileSize!.Value : diff.Summary.RightTotal,
            baseline is null
                ? null
                : useFileSize ? baseline.BinaryFileSize!.Value : diff.Summary.LeftTotal);
    }
}
