namespace Dotsider.Core.Analysis;

/// <summary>
/// Describes how a metadata nesting-chain walk ended.
/// </summary>
internal enum ChainTermination
{
    /// <summary>The chain reached a legal terminal.</summary>
    Complete,

    /// <summary>The chain revisited a metadata row on its active path.</summary>
    Cycle,

    /// <summary>The chain exceeded the supported nesting-depth limit.</summary>
    DepthExceeded,

    /// <summary>The chain contains an invalid row, relationship, terminal, or name.</summary>
    InvalidMetadata,
}
