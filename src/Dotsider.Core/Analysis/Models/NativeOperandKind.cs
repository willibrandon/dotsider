namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The kind of a decoded operand, so consumers (JSON, decoration, diffing) read structure rather
/// than parsing the rendered text.
/// </summary>
public enum NativeOperandKind
{
    /// <summary>A register (GPR, vector, mask, predicate, or FP).</summary>
    Register,

    /// <summary>An immediate constant.</summary>
    Immediate,

    /// <summary>A memory reference (base/index/scale/displacement, or a PC-relative address).</summary>
    Memory,

    /// <summary>A branch/call relative target (a code address).</summary>
    RelativeTarget,
}
