namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The byte totals of one assembly or namespace on both sides of a size diff. Aggregates
/// cover all attributable bytes for their scope — methods, MethodTables, RVA fields, frozen
/// objects via their owner, and (for assemblies) resources — so a scoped size budget measures
/// what the scope actually contributes.
/// </summary>
/// <param name="Name">The assembly simple name or namespace, or <see cref="Dotsider.Core.Analysis.MstatSizeIndex.UnattributedName"/>.</param>
/// <param name="LeftSize">The baseline bytes, or 0 when the scope is new.</param>
/// <param name="RightSize">The comparison-side bytes, or 0 when the scope disappeared.</param>
/// <param name="Delta"><see cref="RightSize"/> minus <see cref="LeftSize"/>.</param>
public sealed record SizeDiffAggregate(
    string Name,
    long LeftSize,
    long RightSize,
    long Delta);
