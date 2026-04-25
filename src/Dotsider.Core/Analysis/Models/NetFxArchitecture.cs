namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Effective process bitness for a .NET Framework root assembly. Models actual runtime
/// architecture, not the PE's compile-time descriptor — AnyCPU is a compile-time attribute
/// that resolves to host bitness at load time, so there is no <c>MSIL</c> runtime arch.
/// </summary>
public enum NetFxArchitecture
{
    /// <summary>32-bit (x86) process.</summary>
    X86,

    /// <summary>64-bit (amd64) process.</summary>
    Amd64,
}
