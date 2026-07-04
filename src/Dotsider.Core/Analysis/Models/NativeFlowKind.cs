namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// How a decoded instruction affects control flow. Drives listing navigation (which instructions
/// carry a jumpable target) and future analysis without re-parsing the mnemonic.
/// </summary>
public enum NativeFlowKind
{
    /// <summary>Falls through to the next instruction.</summary>
    Sequential,

    /// <summary>A direct call to a computed absolute target.</summary>
    Call,

    /// <summary>An unconditional direct jump to a computed absolute target.</summary>
    Jump,

    /// <summary>A conditional branch to a computed absolute target.</summary>
    ConditionalBranch,

    /// <summary>A return from the current function.</summary>
    Return,

    /// <summary>A call through a register or memory operand; the target is not a direct immediate.</summary>
    IndirectCall,

    /// <summary>A jump through a register or memory operand; the target is not a direct immediate.</summary>
    IndirectJump,
}
