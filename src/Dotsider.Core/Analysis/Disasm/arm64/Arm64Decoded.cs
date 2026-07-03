using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm.arm64;

/// <summary>
/// The result of formatting one A64 instruction: the (possibly alias-rewritten) mnemonic, the
/// structured operands, the category and control-flow kind, and the resolved branch target if any.
/// </summary>
/// <param name="Mnemonic">The rendered mnemonic, after alias rewriting.</param>
/// <param name="Operands">The structured operands.</param>
/// <param name="Category">The instruction category.</param>
/// <param name="Flow">The control-flow kind.</param>
/// <param name="Target">The absolute branch/label target, or null.</param>
internal readonly record struct Arm64Decoded(
    string Mnemonic,
    List<NativeOperand> Operands,
    NativeInstructionCategory Category,
    NativeFlowKind Flow,
    ulong? Target);
