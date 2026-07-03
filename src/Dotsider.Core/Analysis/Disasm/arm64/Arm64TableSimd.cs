namespace Dotsider.Core.Analysis.Disasm.arm64;

using static Arm64Format;

/// <summary>
/// Registers the scalar floating-point and Advanced SIMD groups (bits[28:25]=x111): scalar FP
/// arithmetic/compare/convert/select, the vector three-same and two-register-misc classes with
/// their <c>Vd.T</c> arrangements, dup/mod-immediate, the dot-product ops, and the AES/SHA crypto
/// extensions. The formatter derives the arrangement and register widths from the size/Q/type
/// fields.
/// </summary>
internal static partial class Arm64Tables
{
    static partial void RegisterSimdFp()
    {
        RegisterScalarFp();
        RegisterSimd3Same();
        RegisterSimdMisc();
        RegisterCrypto();
    }

    private static void RegisterScalarFp()
    {
        // Scalar FP two-source (fmul/fdiv/fadd/fsub/fmax/fmin/fnmul).
        Add(SimdFp, 0x5F20FC00, 0x1E200800, "fmul", ScalarFp3);
        Add(SimdFp, 0x5F20FC00, 0x1E201800, "fdiv", ScalarFp3);
        Add(SimdFp, 0x5F20FC00, 0x1E202800, "fadd", ScalarFp3);
        Add(SimdFp, 0x5F20FC00, 0x1E203800, "fsub", ScalarFp3);
        Add(SimdFp, 0x5F20FC00, 0x1E204800, "fmax", ScalarFp3);
        Add(SimdFp, 0x5F20FC00, 0x1E205800, "fmin", ScalarFp3);
        Add(SimdFp, 0x5F20FC00, 0x1E208800, "fnmul", ScalarFp3);

        // Scalar FP one-source.
        Add(SimdFp, 0x5F207C00, 0x1E204000, "fmov", ScalarFp2);
        Add(SimdFp, 0x5F207C00, 0x1E20C000, "fabs", ScalarFp2);
        Add(SimdFp, 0x5F207C00, 0x1E214000, "fneg", ScalarFp2);
        Add(SimdFp, 0x5F207C00, 0x1E21C000, "fsqrt", ScalarFp2);
        Add(SimdFp, 0x5F207C00, 0x1E244000, "frintn", ScalarFp2);
        Add(SimdFp, 0x5F207C00, 0x1E254000, "frintp", ScalarFp2);
        Add(SimdFp, 0x5F207C00, 0x1E264000, "frintm", ScalarFp2);
        Add(SimdFp, 0x5F207C00, 0x1E274000, "frintz", ScalarFp2);

        // fcvt between precisions (opcode bits[17:15] pick the target).
        Add(SimdFp, 0x5F3E7C00, 0x1E22C000, "fcvt", FpCvt);
        Add(SimdFp, 0x5F3E7C00, 0x1E23C000, "fcvt", FpCvt);
        Add(SimdFp, 0x5F3E7C00, 0x1EE24000, "fcvt", FpCvt);

        // Scalar FP compare (fcmp / fcmp #0.0).
        Add(SimdFp, 0x5F20FC1F, 0x1E202000, "fcmp", FpCompare);
        Add(SimdFp, 0x5F20FC1F, 0x1E202008, "fcmp", FpCompare);

        // FP conditional select.
        Add(SimdFp, 0x5F200C00, 0x1E200C00, "fcsel", FpCondSelect);

        // Integer<->FP conversions.
        Add(SimdFp, 0x5F3FFC00, 0x1E220000, "scvtf", FpToFromInt);
        Add(SimdFp, 0x5F3FFC00, 0x1E230000, "ucvtf", FpToFromInt);
        Add(SimdFp, 0x5F3FFC00, 0x1E380000, "fcvtzs", FpToFromInt);
        Add(SimdFp, 0x5F3FFC00, 0x1E390000, "fcvtzu", FpToFromInt);
        Add(SimdFp, 0x5F3FFC00, 0x1E260000, "fmov", FpToFromInt); // fmov to/from GPR
        Add(SimdFp, 0x5F3FFC00, 0x1E270000, "fmov", FpToFromInt);
    }

    private static void RegisterSimd3Same()
    {
        // Integer three-same (Vd.T, Vn.T, Vm.T).
        Add(SimdFp, 0xBF20FC00, 0x0E208400, "add", SimdReg3);
        Add(SimdFp, 0xBF20FC00, 0x2E208400, "sub", SimdReg3);
        Add(SimdFp, 0xBF20FC00, 0x0E209C00, "mul", SimdReg3);
        Add(SimdFp, 0xBF20FC00, 0x0E206400, "cmgt", SimdReg3);
        Add(SimdFp, 0xBF20FC00, 0x2E208C00, "cmeq", SimdReg3);
        Add(SimdFp, 0xBF20FC00, 0x0E204400, "smax", SimdReg3);
        Add(SimdFp, 0xBF20FC00, 0x0E204C00, "smin", SimdReg3);
        Add(SimdFp, 0xBF20FC00, 0x2E204400, "umax", SimdReg3);
        Add(SimdFp, 0xBF20FC00, 0x2E204C00, "umin", SimdReg3);

        // Logical (size field selects and/bic/orr/orn/eor/bsl by U and size).
        Add(SimdFp, 0xBFE0FC00, 0x0E201C00, "and", SimdReg3);
        Add(SimdFp, 0xBFE0FC00, 0x0E601C00, "bic", SimdReg3);
        Add(SimdFp, 0xBFE0FC00, 0x0EA01C00, "orr", SimdReg3);
        Add(SimdFp, 0xBFE0FC00, 0x0EE01C00, "orn", SimdReg3);
        Add(SimdFp, 0xBFE0FC00, 0x2E201C00, "eor", SimdReg3);
        Add(SimdFp, 0xBFE0FC00, 0x2E601C00, "bsl", SimdReg3);

        // FP three-same.
        Add(SimdFp, 0xBFA0FC00, 0x0E20D400, "fadd", SimdReg3);
        Add(SimdFp, 0xBFA0FC00, 0x0EA0D400, "fsub", SimdReg3);
        Add(SimdFp, 0xBFA0FC00, 0x2E20DC00, "fmul", SimdReg3);
        Add(SimdFp, 0xBFA0FC00, 0x2E20FC00, "fdiv", SimdReg3);
        Add(SimdFp, 0xBFA0FC00, 0x0E20CC00, "fmla", SimdReg3);
        Add(SimdFp, 0xBFA0FC00, 0x0EA0CC00, "fmls", SimdReg3);
        Add(SimdFp, 0xBFA0FC00, 0x0E20F400, "fmax", SimdReg3);
        Add(SimdFp, 0xBFA0FC00, 0x0EA0F400, "fmin", SimdReg3);

        // Dot product.
        Add(SimdFp, 0xBFE0FC00, 0x0E809400, "sdot", SimdDot);
        Add(SimdFp, 0xBFE0FC00, 0x2E809400, "udot", SimdDot);
    }

    private static void RegisterSimdMisc()
    {
        // Two-register misc (Vd.T, Vn.T).
        Add(SimdFp, 0xBF3FFC00, 0x0E20B800, "abs", SimdMisc2);
        Add(SimdFp, 0xBF3FFC00, 0x2E20B800, "neg", SimdMisc2);
        Add(SimdFp, 0xBFFFFC00, 0x0E205800, "not", SimdMisc2);
        Add(SimdFp, 0xBF3FFC00, 0x0E204800, "cnt", SimdMisc2);
        Add(SimdFp, 0xBFBFFC00, 0x0EA0F800, "fabs", SimdMisc2);
        Add(SimdFp, 0xBFBFFC00, 0x2EA0F800, "fneg", SimdMisc2);
        Add(SimdFp, 0xBFBFFC00, 0x2EA1F800, "fsqrt", SimdMisc2);

        // dup (vector) from a general register.
        Add(SimdFp, 0xBFE0FC00, 0x0E000C00, "dup", SimdDup);

        // Modified immediate (movi/mvni).
        Add(SimdFp, 0xBFF89C00, 0x0F000400, "movi", SimdModImm);
        Add(SimdFp, 0xBFF89C00, 0x2F000400, "mvni", SimdModImm);
    }

    private static void RegisterCrypto()
    {
        // AES round operations (Vd.16b, Vn.16b).
        Add(SimdFp, 0xFFFFFC00, 0x4E284800, "aese", CryptoAes);
        Add(SimdFp, 0xFFFFFC00, 0x4E285800, "aesd", CryptoAes);
        Add(SimdFp, 0xFFFFFC00, 0x4E286800, "aesmc", CryptoAes);
        Add(SimdFp, 0xFFFFFC00, 0x4E287800, "aesimc", CryptoAes);

        // SHA256/SHA1 update (Qd/Vd, …).
        Add(SimdFp, 0xFFE0FC00, 0x5E004000, "sha256h", CryptoSha);
        Add(SimdFp, 0xFFE0FC00, 0x5E005000, "sha256h2", CryptoSha);
        Add(SimdFp, 0xFFFFFC00, 0x5E282800, "sha256su0", CryptoSha);
        Add(SimdFp, 0xFFE0FC00, 0x5E006000, "sha256su1", CryptoSha);
    }
}
