namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The granularity level of a <see cref="SizeNode"/> in the size breakdown tree.
/// </summary>
public enum SizeNodeKind
{
    /// <summary>Root node representing an entire assembly.</summary>
    Assembly,

    /// <summary>Node representing a namespace within an assembly.</summary>
    Namespace,

    /// <summary>Node representing a type within a namespace.</summary>
    Type,

    /// <summary>Node representing a method within a type.</summary>
    Method
}
