namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One decoded operand of a <see cref="NativeInstruction"/>, carried structurally so navigation,
/// JSON/MCP output, syntax decoration, and future diffing never parse the rendered text. The
/// <see cref="Text"/> is the display projection; the typed fields describe what it renders.
/// </summary>
/// <param name="Kind">The operand's kind.</param>
/// <param name="Text">The rendered operand text (e.g. <c>rax</c>, <c>0x10</c>, <c>[rbp-0x8]</c>, <c>zmm1{k1}{z}</c>).</param>
/// <param name="Register">The register name when <see cref="Kind"/> is <see cref="NativeOperandKind.Register"/>, else null.</param>
/// <param name="Immediate">The immediate value when <see cref="Kind"/> is <see cref="NativeOperandKind.Immediate"/>, else null.</param>
/// <param name="MemoryBase">The base register of a memory reference, or null.</param>
/// <param name="MemoryIndex">The index register of a memory reference, or null.</param>
/// <param name="MemoryScale">The index scale (1/2/4/8) of a memory reference, or 0 when there is no index.</param>
/// <param name="MemoryDisplacement">The displacement of a memory reference, or 0.</param>
/// <param name="IsRipRelative">Whether a memory reference is x64 RIP-relative (the displacement is off the next instruction).</param>
public sealed record NativeOperand(
    NativeOperandKind Kind,
    string Text,
    string? Register = null,
    long? Immediate = null,
    string? MemoryBase = null,
    string? MemoryIndex = null,
    int MemoryScale = 0,
    long MemoryDisplacement = 0,
    bool IsRipRelative = false);
