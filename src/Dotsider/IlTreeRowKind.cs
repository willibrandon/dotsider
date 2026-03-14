namespace Dotsider;

/// <summary>
/// The kind of row in the flattened IL tree.
/// </summary>
public enum IlTreeRowKind
{
    /// <summary>A namespace grouping row.</summary>
    Namespace,

    /// <summary>A type definition row.</summary>
    Type,

    /// <summary>A method definition row (leaf).</summary>
    Method
}
