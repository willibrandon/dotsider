// FROZEN TEST FIXTURE — one [MethodImpl(NoInlining)] method per hardware-intrinsic family, guarded
// by IsSupported and fed live inputs so the JIT emits the intrinsic (and trimming cannot elide it).
// The disassembler fixtures assert the expected mnemonics appear per family.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using X86Aes = System.Runtime.Intrinsics.X86.Aes;
using ArmAes = System.Runtime.Intrinsics.Arm.Aes;

var seed = args.Length > 0 && int.TryParse(args[0], out var s) ? s : 3;
long acc = 0;

acc += X64.Run(seed);
acc += Arm.Run(seed);

Console.WriteLine(acc);
return (int)(acc & 0x7F);

/// <summary>x86-64 hardware-intrinsic families, each emitted as its own function.</summary>
internal static class X64
{
    public static long Run(int seed)
    {
        long a = 0;
        if (Sse2.IsSupported) a += Sse2Add(seed);
        if (Avx2.IsSupported) a += Avx2Add(seed);
        if (Fma.IsSupported) a += FmaMul(seed);
        if (Bmi1.IsSupported) a += Bmi1Andn(seed);
        if (Bmi2.IsSupported) a += Bmi2Pdep(seed);
        if (Popcnt.IsSupported) a += PopcntCount(seed);
        if (X86Aes.IsSupported) a += AesEnc(seed);
        if (Avx512F.IsSupported) a += Avx512Add(seed);
        return a;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Sse2Add(int seed)
    {
        var v = Vector128.Create(seed, seed + 1, seed + 2, seed + 3);
        var r = Sse2.Add(v, Sse2.ShiftLeftLogical(v, 1));
        return r.GetElement(0) + r.GetElement(3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Avx2Add(int seed)
    {
        var v = Vector256.Create(seed, 1, 2, 3, 4, 5, 6, 7);
        var r = Avx2.Add(v, Avx2.ShiftLeftLogical(v, (byte)2));
        return Vector256.Sum(r);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long FmaMul(int seed)
    {
        var v = Vector128.Create((float)seed, 1, 2, 3);
        var r = Fma.MultiplyAdd(v, v, v);
        return (long)r.GetElement(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Bmi1Andn(int seed) => Bmi1.AndNot((uint)seed, (uint)(seed << 3));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Bmi2Pdep(int seed) => Bmi2.ParallelBitDeposit((uint)seed, 0xF0F0F0F0u);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long PopcntCount(int seed) => Popcnt.PopCount((uint)(seed * 2654435761u));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long AesEnc(int seed)
    {
        var v = Vector128.Create(seed).AsByte();
        var r = X86Aes.Encrypt(v, v);
        return r.GetElement(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Avx512Add(int seed)
    {
        var v = Vector512.Create(seed, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
        var r = Avx512F.Add(v, v);
        return Vector512.Sum(r);
    }
}

/// <summary>arm64 hardware-intrinsic families (executed only on the AArch64 leg).</summary>
internal static class Arm
{
    public static long Run(int seed)
    {
        long a = 0;
        if (AdvSimd.IsSupported) a += AdvSimdAdd(seed);
        if (Crc32.IsSupported) a += Crc(seed);
        if (ArmAes.IsSupported) a += AesEnc(seed);
        if (Sve.IsSupported) a += SveAdd(seed);
        return a;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long AdvSimdAdd(int seed)
    {
        var v = Vector128.Create(seed, seed + 1, seed + 2, seed + 3);
        var r = AdvSimd.Add(v, AdvSimd.Multiply(v, v));
        return r.GetElement(0) + r.GetElement(3);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long Crc(int seed) => Crc32.ComputeCrc32((uint)seed, (uint)(seed * 7));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long AesEnc(int seed)
    {
        var v = Vector128.Create(seed).AsByte();
        var r = ArmAes.Encrypt(v, v);
        return r.GetElement(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long SveAdd(int seed)
    {
        var v = new Vector<int>(seed);
        var r = Sve.Add(v, v);
        return r[0];
    }
}
