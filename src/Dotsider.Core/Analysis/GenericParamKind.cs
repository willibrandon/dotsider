namespace Dotsider.Core.Analysis;

/// <summary>
/// Identifies whether a decoded generic parameter belongs to a type or a method.
/// </summary>
internal enum GenericParamKind
{
    /// <summary>The parameter belongs to the enclosing type.</summary>
    TypeParameter,

    /// <summary>The parameter belongs to the enclosing method.</summary>
    MethodParameter,
}
