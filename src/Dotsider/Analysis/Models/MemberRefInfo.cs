namespace Dotsider.Analysis.Models;

/// <summary>
/// Information about a referenced member (method or field) from the MemberRef metadata table.
/// </summary>
/// <param name="Token">The metadata token for this member reference.</param>
/// <param name="DeclaringType">The fully qualified name of the type that declares this member.</param>
/// <param name="Name">The name of the referenced member.</param>
/// <param name="Signature">The decoded signature of the member.</param>
public sealed record MemberRefInfo(
    int Token,
    string DeclaringType,
    string Name,
    string Signature);
