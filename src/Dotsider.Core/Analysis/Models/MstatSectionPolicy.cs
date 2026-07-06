namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Decides which of an mstat's 2.1+ detail sections carry the bytes that the format
/// double-reports into blob buckets. Every 2.x report sums frozen object, field RVA, and
/// resource bytes into the <c>ArrayOfFrozenObjects</c>, <c>FieldRvaData</c>, and
/// <c>ResourceData</c> blobs for back-compat; a reader must pick, per section, either the
/// detail entries or the bucket blob — never both. Sharing this policy between
/// <see cref="Dotsider.Core.Analysis.SizeAnalyzer"/>, <see cref="Dotsider.Core.Analysis.MstatSizeIndex"/>,
/// and <see cref="Dotsider.Core.Analysis.MstatDiffer"/> is what keeps their totals identical.
/// </summary>
/// <param name="UseFrozenObjects">True to take frozen objects from the detail section and exclude the <c>ArrayOfFrozenObjects</c> blob.</param>
/// <param name="UseRvaFields">True to take field RVA data from the detail section and exclude the <c>FieldRvaData</c> blob.</param>
/// <param name="UseManifestResources">True to take resources from the detail section and exclude the <c>ResourceData</c> blob.</param>
public readonly record struct MstatSectionPolicy(
    bool UseFrozenObjects,
    bool UseRvaFields,
    bool UseManifestResources)
{
    /// <summary>
    /// The policy for reading one report on its own: each detail section is used when it has
    /// entries. A 1.x report has empty detail sections, so everything stays at blob fidelity.
    /// </summary>
    /// <param name="data">The report to derive the policy from.</param>
    /// <returns>The single-report policy.</returns>
    public static MstatSectionPolicy ForData(MstatData data) => new(
        data.FrozenObjects.Count > 0,
        data.RvaFields.Count > 0,
        data.ManifestResources.Count > 0);

    /// <summary>
    /// The policy for comparing two reports, applied to both sides so the same bytes land in
    /// the same section everywhere. A detail section is used only when every non-empty side
    /// understands it (format 2.1+) and at least one side has entries; otherwise both sides
    /// degrade to blob fidelity for that section, which loses no bytes because 2.x
    /// double-reports them. <see cref="MstatData.Empty"/> (format 0.0) is transparent: it
    /// constrains nothing.
    /// </summary>
    /// <param name="left">The baseline report.</param>
    /// <param name="right">The report under comparison.</param>
    /// <returns>The shared policy for both sides.</returns>
    public static MstatSectionPolicy ForPair(MstatData left, MstatData right)
    {
        static bool SupportsDetail(MstatData d) =>
            d.FormatMajorVersion == 0
            || d.FormatMajorVersion > 2
            || (d.FormatMajorVersion == 2 && d.FormatMinorVersion >= 1);

        var compatible = SupportsDetail(left) && SupportsDetail(right);
        return new MstatSectionPolicy(
            compatible && (left.FrozenObjects.Count > 0 || right.FrozenObjects.Count > 0),
            compatible && (left.RvaFields.Count > 0 || right.RvaFields.Count > 0),
            compatible && (left.ManifestResources.Count > 0 || right.ManifestResources.Count > 0));
    }

    /// <summary>
    /// The blob names this policy excludes — the buckets whose bytes are read from a detail
    /// section instead.
    /// </summary>
    /// <returns>The excluded blob names.</returns>
    public IReadOnlySet<string> ExcludedBlobNames()
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        if (UseFrozenObjects) excluded.Add("ArrayOfFrozenObjects");
        if (UseRvaFields) excluded.Add("FieldRvaData");
        if (UseManifestResources) excluded.Add("ResourceData");
        return excluded;
    }
}
