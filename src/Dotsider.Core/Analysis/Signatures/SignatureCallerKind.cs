namespace Dotsider.Core.Analysis.Signatures;

/// <summary>
/// Identifies the metadata entity or embedded production that owns a method-shaped signature.
/// </summary>
internal enum SignatureCallerKind
{
    /// <summary>A signature owned by a MethodDef row.</summary>
    MethodDefinition,

    /// <summary>A method signature owned by a MemberRef row.</summary>
    MemberReference,

    /// <summary>A call-site signature owned by a StandAloneSig row.</summary>
    StandaloneSignature,

    /// <summary>A method signature embedded in a function-pointer type.</summary>
    FunctionPointer,

    /// <summary>A signature owned by a Property row.</summary>
    PropertyDefinition,
}
