namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The granularity level of a <see cref="SizeNode"/> in the size breakdown tree. The kinds
/// beyond <see cref="Method"/> appear only in Native AOT trees built from an mstat report.
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
    Method,

    /// <summary>Grouping node for a Native AOT data category (blobs, frozen objects, and the like).</summary>
    Category,

    /// <summary>A named global data region of a Native AOT binary.</summary>
    Blob,

    /// <summary>A type's runtime MethodTable data in a Native AOT binary.</summary>
    MethodTable,

    /// <summary>An object frozen into a Native AOT binary at compile time.</summary>
    FrozenObject,

    /// <summary>A field's RVA data mapped into a Native AOT binary.</summary>
    RvaField,

    /// <summary>A manifest resource embedded in a Native AOT binary.</summary>
    Resource
}
