namespace Dotsider.Core.Analysis;

/// <summary>
/// Identifies the encoded operand shape associated with an IL opcode.
/// </summary>
internal enum OperandKind
{
    /// <summary>The opcode has no encoded operand.</summary>
    None,

    /// <summary>The opcode has a one-byte relative branch target.</summary>
    ShortBranchTarget,

    /// <summary>The opcode has a four-byte relative branch target.</summary>
    BranchTarget,

    /// <summary>The opcode has a one-byte integer operand.</summary>
    ShortInlineI,

    /// <summary>The opcode has a four-byte integer operand.</summary>
    InlineI,

    /// <summary>The opcode has an eight-byte integer operand.</summary>
    InlineI8,

    /// <summary>The opcode has a four-byte floating-point operand.</summary>
    ShortInlineR,

    /// <summary>The opcode has an eight-byte floating-point operand.</summary>
    InlineR,

    /// <summary>The opcode has a one-byte variable index.</summary>
    ShortInlineVar,

    /// <summary>The opcode has a two-byte variable index.</summary>
    InlineVar,

    /// <summary>The opcode has a four-byte user-string token.</summary>
    InlineString,

    /// <summary>The opcode has a four-byte method token.</summary>
    InlineMethod,

    /// <summary>The opcode has a four-byte field token.</summary>
    InlineField,

    /// <summary>The opcode has a four-byte type token.</summary>
    InlineType,

    /// <summary>The opcode has a four-byte member token.</summary>
    InlineTok,

    /// <summary>The opcode has a four-byte standalone-signature token.</summary>
    InlineSig,

    /// <summary>The opcode has a count followed by four-byte switch targets.</summary>
    InlineSwitch
}
