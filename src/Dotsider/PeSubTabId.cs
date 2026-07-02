namespace Dotsider;

/// <summary>
/// Sub-tab index constants for the PE/Metadata tab.
/// </summary>
public static class PeSubTabId
{
    /// <summary>Sections sub-tab.</summary>
    public const int Sections = 0;

    /// <summary>TypeDef sub-tab.</summary>
    public const int TypeDef = 1;

    /// <summary>MethodDef sub-tab.</summary>
    public const int MethodDef = 2;

    /// <summary>TypeRef sub-tab.</summary>
    public const int TypeRef = 3;

    /// <summary>MemberRef sub-tab.</summary>
    public const int MemberRef = 4;

    /// <summary>Attributes sub-tab.</summary>
    public const int Attributes = 5;

    /// <summary>Resources sub-tab.</summary>
    public const int Resources = 6;

    /// <summary>Debug Directory sub-tab.</summary>
    public const int DebugDirectory = 7;

    /// <summary>Imports sub-tab (native import table; needs no CLR header).</summary>
    public const int Imports = 8;

    /// <summary>Exports sub-tab (native export table; needs no CLR header).</summary>
    public const int Exports = 9;

    /// <summary>Load Config sub-tab (load configuration directory; needs no CLR header).</summary>
    public const int LoadConfig = 10;

    /// <summary>R2R Sections sub-tab (Native AOT ReadyToRun section table).</summary>
    public const int RtrSections = 11;

    /// <summary>AOT Types sub-tab (types and methods recovered from Native AOT metadata).</summary>
    public const int AotTypes = 12;

    /// <summary>Total number of PE/Metadata sub-tabs.</summary>
    public const int Count = 13;
}
