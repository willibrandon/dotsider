namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Distinguishes whether a MemberRef entry refers to a method or a field.
/// </summary>
public enum MemberRefKind
{
    /// <summary>The member reference is a method.</summary>
    Method,

    /// <summary>The member reference is a field.</summary>
    Field
}
