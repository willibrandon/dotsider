namespace Dotsider.Core.Analysis.Signatures;

/// <summary>
/// Describes the context-sensitive prefixes and terminal types accepted at a signature position.
/// </summary>
[Flags]
internal enum SignatureTypeContext
{
    /// <summary>No context-specific prefix or terminal is accepted.</summary>
    None = 0,

    /// <summary>Custom modifiers may prefix the type.</summary>
    CustomModifiers = 1 << 0,

    /// <summary>A managed-pointer prefix may wrap the type.</summary>
    ByReference = 1 << 1,

    /// <summary>A pinned constraint may wrap the type.</summary>
    Pinned = 1 << 2,

    /// <summary>The typed-reference terminal type is accepted.</summary>
    TypedReference = 1 << 3,

    /// <summary>The void terminal type is accepted.</summary>
    Void = 1 << 4,
}
