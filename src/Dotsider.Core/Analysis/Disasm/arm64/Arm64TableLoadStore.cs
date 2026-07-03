namespace Dotsider.Core.Analysis.Disasm.arm64;

using static Arm64Format;

/// <summary>
/// Registers the A64 load/store groups. Rather than a row per size/opc combination, each addressing
/// mode is one class row whose mnemonic and register width the formatter derives from the size, V
/// (SIMD/FP), and opc fields — covering the register offsets (unsigned/unscaled/pre/post/register),
/// pairs (<c>stp</c>/<c>ldp</c> incl. the prologue/epilogue pre/post-index forms), PC-relative
/// literals, exclusive and acquire/release accesses, and the LSE atomics.
/// </summary>
internal static partial class Arm64Tables
{
    static partial void RegisterLoadStore()
    {
        // Register + immediate offsets (the [29:27]=111 class, split by bits[25:24] and [11:10]).
        Add(LoadStore, 0x3B000000, 0x39000000, "", LdStUImm);          // unsigned scaled offset
        Add(LoadStore, 0x3B200C00, 0x38000000, "", LdStUnscaled);      // ldur/stur (unscaled)
        Add(LoadStore, 0x3B200C00, 0x38000400, "", LdStImmIndexed);    // post-index
        Add(LoadStore, 0x3B200C00, 0x38000C00, "", LdStImmIndexed);    // pre-index
        Add(LoadStore, 0x3B200C00, 0x38200800, "", LdStRegOff);        // register offset
        Add(LoadStore, 0x3B200C00, 0x38200000, "", Atomic);           // LSE atomic

        // Register pairs (the [29:27]=101 class, split by bits[24:23]).
        Add(LoadStore, 0x3B800000, 0x29000000, "", LdStPair);          // signed offset
        Add(LoadStore, 0x3B800000, 0x28800000, "", LdStPair);          // post-index
        Add(LoadStore, 0x3B800000, 0x29800000, "", LdStPair);          // pre-index

        // PC-relative literal load.
        Add(LoadStore, 0x3B000000, 0x18000000, "", LdLiteral);

        // Exclusive and load-acquire/store-release.
        Add(LoadStore, 0x3F000000, 0x08000000, "", LdStExclusive);
    }
}
