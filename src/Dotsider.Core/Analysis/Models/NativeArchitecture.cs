namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The instruction-set architecture a native code window is decoded as. Carried on
/// <see cref="NativeSymbolInfo"/> from the real image (or the selected fat-Mach-O slice) so the
/// disassembler never has to guess from an ambiguous machine string.
/// </summary>
public enum NativeArchitecture
{
    /// <summary>The architecture could not be determined (managed or unrecognized image).</summary>
    Unknown,

    /// <summary>x86-64 (AMD64 / Intel 64). Disassembly supported.</summary>
    X64,

    /// <summary>AArch64 (ARM64). Disassembly supported.</summary>
    Arm64,

    /// <summary>x86 (32-bit). Disassembly supported.</summary>
    X86,

    /// <summary>ARM 32-bit (Thumb-2). Disassembly supported.</summary>
    Arm32,

    /// <summary>RISC-V 64-bit. Disassembly supported.</summary>
    RiscV64,

    /// <summary>LoongArch 64-bit. Disassembly supported.</summary>
    LoongArch64,

    /// <summary>WebAssembly 32-bit. Disassembly supported.</summary>
    Wasm32,
}
