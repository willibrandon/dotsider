namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One decoded native instruction. The model is structured — bytes, structured operands, flow and
/// target metadata, and source attribution — so navigation, JSON/MCP output, syntax decoration,
/// and future diffing read facts rather than parse text. <see cref="OperandText"/> and the
/// rendered listing line are projections; <see cref="Address"/> is the semantic key and
/// <see cref="DisplayLine"/> the presentation key, mirroring
/// <see cref="IlInstruction"/> for the shared IL-Inspector plumbing.
/// </summary>
/// <param name="Address">The instruction's virtual address.</param>
/// <param name="Rva">The PE relative virtual address, or null for non-PE images.</param>
/// <param name="FileOffset">The file offset of the instruction's bytes, or null when not file-backed.</param>
/// <param name="Bytes">The raw encoded bytes of exactly this instruction.</param>
/// <param name="Length">The encoded byte length (always exact, even for the fallback).</param>
/// <param name="Mnemonic">The instruction mnemonic (e.g. <c>mov</c>, <c>vaddps</c>, <c>bl</c>, or <c>.byte</c>/<c>.word</c> for the fallback).</param>
/// <param name="Operands">The structured operands, in source order.</param>
/// <param name="OperandText">The rendered operand string, or empty when there are none.</param>
/// <param name="Category">The instruction's coarse category.</param>
/// <param name="Flow">How the instruction affects control flow.</param>
/// <param name="TargetAddress">The resolved absolute call/branch/data target, or null.</param>
/// <param name="TargetKind">What <paramref name="TargetAddress"/> points at.</param>
/// <param name="TargetName">The resolved target's display name (e.g. <c>Foo</c>, <c>Foo+0x12</c>, <c>loc_140001234</c>), or null.</param>
/// <param name="SourceFile">The source file for this address from the native source map, or null.</param>
/// <param name="Line">The source line for this address, or null.</param>
/// <param name="IsFallback">Whether this is a <c>.byte</c>/<c>.word</c> safety-net entry for undefined or corrupt bytes.</param>
/// <param name="DisplayLine">The 1-based rendered line number in formatted disassembly, or null.</param>
/// <param name="Layout">The rendered-line column spans, set by the text formatter, or null.</param>
public sealed record NativeInstruction(
    ulong Address,
    uint? Rva,
    long? FileOffset,
    IReadOnlyList<byte> Bytes,
    int Length,
    string Mnemonic,
    IReadOnlyList<NativeOperand> Operands,
    string OperandText,
    NativeInstructionCategory Category,
    NativeFlowKind Flow,
    ulong? TargetAddress = null,
    NativeTargetKind TargetKind = NativeTargetKind.None,
    string? TargetName = null,
    string? SourceFile = null,
    int? Line = null,
    bool IsFallback = false,
    int? DisplayLine = null,
    NativeLineLayout? Layout = null);
