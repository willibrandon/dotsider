using System.Runtime.CompilerServices;

namespace ReadyToRunComponentLib;

/// <summary>
/// A component-assembly type whose methods a composite ReadyToRun image precompiles into the
/// composite executable — exercising component-assembly resolution by name and MVID.
/// </summary>
public static class Calculator
{
    /// <summary>Adds two integers.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Add(int a, int b) => a + b;

    /// <summary>Multiplies two integers.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Multiply(int a, int b) => a * b;
}
