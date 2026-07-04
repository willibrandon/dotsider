using Dotsider.Core.Analysis.Models;

namespace Dotsider;

/// <summary>
/// A single row in the flattened IL Inspector tree, built from namespace → type → method hierarchy.
/// Used as the data model for the ListWidget-based tree.
/// </summary>
/// <param name="Key">Stable row identity for focus tracking.</param>
/// <param name="Depth">Nesting depth (0 = namespace, 1 = type, 2 = method).</param>
/// <param name="Kind">Whether this row represents a namespace, type, or method.</param>
/// <param name="Label">Display text (without indentation — indentation is rendered from <see cref="Depth"/>).</param>
/// <param name="Method">The method definition, if <see cref="Kind"/> is <see cref="IlTreeRowKind.Method"/>.</param>
/// <param name="CanExpand">Whether this row can be expanded (namespaces and types).</param>
/// <param name="IsExpanded">Whether this row is currently expanded.</param>
/// <param name="ExpansionKey">Key into <see cref="DotsiderState.IlTreeExpansionState"/> for toggling.</param>
/// <param name="Symbol">The native symbol this row represents in native (non-managed) mode, or null in managed mode.</param>
public sealed record IlTreeRow(
    string Key,
    int Depth,
    IlTreeRowKind Kind,
    string Label,
    MethodDefInfo? Method,
    bool CanExpand,
    bool IsExpanded,
    string ExpansionKey,
    NativeSymbol? Symbol = null);
