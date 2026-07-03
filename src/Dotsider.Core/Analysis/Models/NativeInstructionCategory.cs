namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A coarse classification of a decoded instruction by function, for grouping, coloring, and
/// summaries without inspecting the mnemonic string.
/// </summary>
public enum NativeInstructionCategory
{
    /// <summary>Scalar integer / general-purpose data-processing (mov, add, cmp, lea, …).</summary>
    Integer,

    /// <summary>Control flow (call, jmp, jcc, ret, branch).</summary>
    Control,

    /// <summary>Vector / SIMD (SSE–AVX-512, AdvSIMD, SVE).</summary>
    Vector,

    /// <summary>Scalar floating point (x87, scalar SSE/AVX float, arm64 FP).</summary>
    Float,

    /// <summary>System / privileged / runtime (nop, int3, ud2, fences, cpuid, barriers, mrs/msr).</summary>
    System,

    /// <summary>Cryptographic / hash (AES, PCLMUL, SHA, CRC32).</summary>
    Crypto,

    /// <summary>A safety-net fallback (<c>.byte</c>/<c>.word</c>) for undefined or corrupt bytes.</summary>
    Unknown,
}
