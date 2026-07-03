namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// What a decoded instruction's resolved <see cref="NativeInstruction.TargetAddress"/> points at,
/// so the view can style and navigate a call/branch/data reference correctly.
/// </summary>
public enum NativeTargetKind
{
    /// <summary>No resolvable target.</summary>
    None,

    /// <summary>A function symbol (possibly at a non-zero offset into it).</summary>
    Function,

    /// <summary>A data symbol (RIP-relative data reference, ADRP/ADR materialization).</summary>
    Data,

    /// <summary>An imported symbol reached through the IAT, PLT/GOT, or a Mach-O stub.</summary>
    Import,

    /// <summary>A synthesized label for a target inside the current function.</summary>
    LocalLabel,

    /// <summary>A computed target that resolved to no known symbol.</summary>
    Unresolved,
}
