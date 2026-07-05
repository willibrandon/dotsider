namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The instruction-set architecture a native code window is decoded as. Carried on
/// <see cref="NativeSymbolInfo"/> from the real image (or the selected fat-Mach-O slice) so the
/// disassembler never has to guess from an ambiguous machine string.
/// </summary>
/// <remarks>
/// Only <see cref="X64"/> and <see cref="Arm64"/> have decoders. The remaining values are
/// report-only: an image (for example a ReadyToRun binary) can identify its real machine so the
/// UI can say "disassembly unsupported for {arch}" rather than misreport it as
/// <see cref="Unknown"/> or imply the code is absent.
/// </remarks>
public enum NativeArchitecture
{
    /// <summary>The architecture could not be determined (managed or unrecognized image).</summary>
    Unknown,

    /// <summary>x86-64 (AMD64 / Intel 64). Disassembly supported.</summary>
    X64,

    /// <summary>AArch64 (ARM64). Disassembly supported.</summary>
    Arm64,

    /// <summary>x86 (32-bit). Report-only; disassembly unsupported.</summary>
    X86,

    /// <summary>ARM 32-bit (Thumb-2). Report-only; disassembly unsupported.</summary>
    Arm32,

    /// <summary>RISC-V 64-bit. Report-only; disassembly unsupported.</summary>
    RiscV64,

    /// <summary>LoongArch 64-bit. Report-only; disassembly unsupported.</summary>
    LoongArch64,

    /// <summary>WebAssembly 32-bit. Report-only; disassembly unsupported.</summary>
    Wasm32,
}
