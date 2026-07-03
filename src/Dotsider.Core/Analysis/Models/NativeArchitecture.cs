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

    /// <summary>x86-64 (AMD64 / Intel 64).</summary>
    X64,

    /// <summary>AArch64 (ARM64).</summary>
    Arm64,
}
