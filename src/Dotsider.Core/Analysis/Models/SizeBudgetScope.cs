namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// What a size budget measures: the whole binary, one namespace subtree, or one assembly.
/// </summary>
public enum SizeBudgetScope
{
    /// <summary>The build's total size.</summary>
    Total,

    /// <summary>
    /// A namespace and everything beneath it: a target of <c>System.Text.Json</c> covers
    /// <c>System.Text.Json.Serialization</c> but not <c>System.Text.Json2</c>.
    /// </summary>
    Namespace,

    /// <summary>One assembly, matched by simple name.</summary>
    Assembly
}
