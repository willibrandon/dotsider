namespace Dotsider.Core.Analysis;

/// <summary>
/// Describes an mstat method or field name, its declaring type, and its overload signature.
/// </summary>
internal readonly record struct MemberAttribution
{
    /// <summary>Initializes a member attribution.</summary>
    /// <param name="name">The member name.</param>
    /// <param name="type">The declaring type attribution.</param>
    /// <param name="signature">The rendered overload signature.</param>
    public MemberAttribution(string name, TypeAttribution type, string signature = "")
    {
        Name = name;
        Type = type;
        Signature = signature;
    }

    /// <summary>Gets the member name.</summary>
    public string Name { get; init; }

    /// <summary>Gets the declaring type attribution.</summary>
    public TypeAttribution Type { get; init; }

    /// <summary>Gets the rendered overload signature.</summary>
    public string Signature { get; init; }
}
