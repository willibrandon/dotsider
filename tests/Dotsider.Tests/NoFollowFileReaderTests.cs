using Dotsider.Core.Analysis;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Verifies Linux no-follow opens use the ABI constants for each architecture.
/// The mapping prevents final-path symbolic links from being followed during analysis.
/// Unknown architectures must fail closed.
/// </summary>
[TestClass]
public sealed class NoFollowFileReaderTests
{
    /// <summary>
    /// Verifies every supported Linux architecture gets its ABI-specific no-follow value.
    /// Close-on-exec and non-blocking flags remain present on every architecture.
    /// </summary>
    [TestMethod]
    [DataRow(Architecture.Arm, 0x0000_8000)]
    [DataRow(Architecture.Arm64, 0x0000_8000)]
    [DataRow(Architecture.Armv6, 0x0000_8000)]
    [DataRow(Architecture.Ppc64le, 0x0000_8000)]
    [DataRow(Architecture.LoongArch64, 0x0002_0000)]
    [DataRow(Architecture.RiscV64, 0x0002_0000)]
    [DataRow(Architecture.S390x, 0x0002_0000)]
    [DataRow(Architecture.X64, 0x0002_0000)]
    [DataRow(Architecture.X86, 0x0002_0000)]
    public void TryGetLinuxOpenFlags_KnownArchitecture_UsesExpectedNoFollowFlag(
        Architecture architecture,
        int expectedNoFollowFlag)
    {
        const int openCloseOnExec = 0x0008_0000;
        const int openNonBlocking = 0x0000_0800;

        bool supported = NoFollowFileReader.TryGetLinuxOpenFlags(architecture, out int flags);

        Assert.IsTrue(supported);
        Assert.AreEqual(openCloseOnExec | expectedNoFollowFlag | openNonBlocking, flags);
    }

    /// <summary>
    /// Verifies an architecture without known Linux ABI constants cannot open a path.
    /// The returned flags remain empty so callers cannot accidentally perform an unsafe fallback.
    /// </summary>
    [TestMethod]
    public void TryGetLinuxOpenFlags_UnknownArchitecture_FailsClosed()
    {
        bool supported = NoFollowFileReader.TryGetLinuxOpenFlags(Architecture.Wasm, out int flags);

        Assert.IsFalse(supported);
        Assert.AreEqual(0, flags);
    }
}
