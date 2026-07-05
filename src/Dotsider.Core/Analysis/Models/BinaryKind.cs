namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Coarse classification of an analyzed binary.
/// </summary>
public enum BinaryKind
{
    /// <summary>A managed assembly with ECMA-335 metadata.</summary>
    Managed,

    /// <summary>
    /// A crossgen2 ReadyToRun image: full ECMA-335 metadata plus precompiled native method
    /// bodies (non-composite, composite, or a composite component). Every managed tab works;
    /// the native bodies are additionally correlated to their managed methods.
    /// </summary>
    ReadyToRun,

    /// <summary>
    /// A Native AOT compiled .NET binary: a native executable with no CLR metadata
    /// whose image embeds a validated ReadyToRun header.
    /// </summary>
    NativeAot,

    /// <summary>A native binary with no CLR metadata and no ReadyToRun header (apphost, unknown format).</summary>
    Native,
}
