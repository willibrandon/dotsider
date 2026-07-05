namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// What a <see cref="ReadyToRunCodeRange"/> represents within a precompiled method's body. An R2R
/// method owns one hot entry, zero or more funclets, and an optional disjoint cold range.
/// </summary>
public enum ReadyToRunCodeRangeKind
{
    /// <summary>The method's hot entry point — the range its entry runtime function starts.</summary>
    HotEntry,

    /// <summary>A funclet (exception handler / filter) that follows the hot entry.</summary>
    Funclet,

    /// <summary>The method's cold range, split out via the hot/cold map.</summary>
    Cold,
}
