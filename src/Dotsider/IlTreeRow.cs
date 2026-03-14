using Dotsider.Core.Analysis.Models;

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

/// <summary>
/// A single row in the flattened IL Inspector tree, built from namespace → type → method hierarchy.
/// Used as the data model for the TableWidget-based tree replacement.
/// </summary>
/// <param name="Key">Stable row identity for TableWidget focus tracking.</param>
/// <param name="Depth">Nesting depth (0 = namespace, 1 = type, 2 = method).</param>
/// <param name="Kind">Whether this row represents a namespace, type, or method.</param>
/// <param name="Label">Display text (without indentation — indentation is rendered from <see cref="Depth"/>).</param>
/// <param name="Method">The method definition, if <see cref="Kind"/> is <see cref="IlTreeRowKind.Method"/>.</param>
/// <param name="CanExpand">Whether this row can be expanded (namespaces and types).</param>
/// <param name="IsExpanded">Whether this row is currently expanded.</param>
/// <param name="ExpansionKey">Key into <see cref="DotsiderState.IlTreeExpansionState"/> for toggling.</param>
public sealed record IlTreeRow(
    string Key,
    int Depth,
    IlTreeRowKind Kind,
    string Label,
    MethodDefInfo? Method,
    bool CanExpand,
    bool IsExpanded,
    string ExpansionKey);
