using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm;

/// <summary>
/// Registers the native instruction decoders dotsider can dispatch to.
/// This is the single support table for architecture-specific native disassembly.
/// Unknown or newly-added architectures are not implicitly routed to another decoder.
/// </summary>
internal static class NativeDecoderRegistry
{
    private static readonly IReadOnlyDictionary<NativeArchitecture, Decoder> Decoders =
        new Dictionary<NativeArchitecture, Decoder>
        {
            [NativeArchitecture.X64] = x64.XarchDecoder.Decode,
            [NativeArchitecture.Arm64] = arm64.Arm64Decoder.Decode,
            [NativeArchitecture.X86] = x86.X86Decoder.Decode,
            [NativeArchitecture.Arm32] = arm32.Arm32ThumbDecoder.Decode,
            [NativeArchitecture.RiscV64] = riscv64.RiscV64Decoder.Decode,
            [NativeArchitecture.LoongArch64] = loongarch64.LoongArch64Decoder.Decode,
            [NativeArchitecture.Wasm32] = wasm32.Wasm32Decoder.Decode,
        };

    /// <summary>
    /// Gets every architecture with a registered decoder.
    /// The collection is the authoritative source for disassembly support checks.
    /// Tests assert that every concrete <see cref="NativeArchitecture"/> value is represented.
    /// </summary>
    internal static IReadOnlyCollection<NativeArchitecture> SupportedArchitectures => [.. Decoders.Keys];

    /// <summary>
    /// Returns whether a decoder is registered for the architecture.
    /// <see cref="NativeArchitecture.Unknown"/> is intentionally unsupported.
    /// Newly-added enum values must add a row here before they decode.
    /// </summary>
    internal static bool IsSupported(NativeArchitecture architecture) =>
        Decoders.ContainsKey(architecture);

    /// <summary>
    /// Decodes one instruction using the registered decoder.
    /// The return value is false when no decoder is registered.
    /// Callers then emit honest fallback bytes instead of guessing an architecture.
    /// </summary>
    internal static bool TryDecode(
        NativeArchitecture architecture,
        ReadOnlySpan<byte> code,
        int offset,
        ulong baseAddress,
        out NativeInstruction instruction)
    {
        if (Decoders.TryGetValue(architecture, out var decoder))
        {
            instruction = decoder(code, offset, baseAddress + (ulong)offset);
            return true;
        }

        instruction = default!;
        return false;
    }

    private delegate NativeInstruction Decoder(ReadOnlySpan<byte> code, int offset, ulong address);
}
