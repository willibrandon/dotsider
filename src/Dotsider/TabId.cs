namespace Dotsider;

/// <summary>
/// Tab index constants for the main dotsider application.
/// Avoids magic numbers — views reference <c>state.Search[TabId.X]</c>.
/// If tabs are reordered, only these constants need updating.
/// </summary>
public static class TabId
{
    /// <summary>Tab index for the General assembly overview.</summary>
    public const int General = 0;

    /// <summary>Tab index for the PE/Metadata inspector.</summary>
    public const int PeMetadata = 1;

    /// <summary>Tab index for the IL Inspector.</summary>
    public const int IlInspector = 2;

    /// <summary>Tab index for the Strings viewer.</summary>
    public const int Strings = 3;

    /// <summary>Tab index for the Hex Dump editor.</summary>
    public const int HexDump = 4;

    /// <summary>Tab index for the Dependency Graph.</summary>
    public const int DepGraph = 5;

    /// <summary>Tab index for the Size Treemap.</summary>
    public const int SizeMap = 6;

    /// <summary>Tab index for the Dynamic Analysis tracer.</summary>
    public const int Dynamic = 7;
}
