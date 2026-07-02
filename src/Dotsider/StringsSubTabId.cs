namespace Dotsider;

/// <summary>
/// Sub-tab index constants for the Strings tab.
/// </summary>
public static class StringsSubTabId
{
    /// <summary>User Strings (#US) sub-tab.</summary>
    public const int UserStrings = 0;

    /// <summary>Metadata (#Strings) sub-tab.</summary>
    public const int Metadata = 1;

    /// <summary>Raw Binary sub-tab.</summary>
    public const int RawBinary = 2;

    /// <summary>Raw UTF-16 sub-tab.</summary>
    public const int RawBinaryUtf16 = 3;

    /// <summary>Frozen (AOT) sub-tab — frozen string literals from a Native AOT binary.</summary>
    public const int FrozenObject = 4;

    /// <summary>Total number of Strings sub-tabs.</summary>
    public const int Count = 5;
}
