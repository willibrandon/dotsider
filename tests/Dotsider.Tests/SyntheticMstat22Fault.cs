namespace Dotsider.Tests;

/// <summary>
/// Identifies one precise corruption to apply to the final deduplicated-method row in a
/// synthetic format-2.2 mstat image.
/// </summary>
internal enum SyntheticMstat22Fault
{
    /// <summary>No corruption is applied.</summary>
    None,

    /// <summary>The four-byte count operand ends before all operand bytes are present.</summary>
    TruncatedCount,

    /// <summary>The final target pair ends immediately after its token.</summary>
    TruncatedTargetNameOffset,

    /// <summary>The final target token starts with an unsupported opcode.</summary>
    MalformedTargetToken,

    /// <summary>The final target name offset starts with an unsupported opcode.</summary>
    MalformedTargetNameOffset,

    /// <summary>The final target name offset lies outside the serialized-name section.</summary>
    OutOfRangeTargetNameOffset,
}
