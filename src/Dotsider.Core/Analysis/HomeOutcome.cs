namespace Dotsider.Core.Analysis;

/// <summary>
/// Describes how type-forwarder resolution determined the declaring type's home.
/// </summary>
internal enum HomeOutcome
{
    /// <summary>An assembly owning the declaring type as a TypeDef was reached.</summary>
    Found,

    /// <summary>The starting assembly neither owns nor forwards the declaring type.</summary>
    NotFound,

    /// <summary>A forwarding chain was found but could not reach an owning assembly.</summary>
    ChaseBroken,
}
