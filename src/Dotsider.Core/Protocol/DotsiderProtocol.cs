namespace Dotsider.Core.Protocol;

/// <summary>
/// Constants for the dotsider diagnostics protocol.
/// </summary>
public static class DotsiderProtocol
{
    /// <summary>
    /// Maximum UTF-8 byte length of a diagnostics request payload, excluding
    /// an optional UTF-8 byte-order mark and the line delimiter.
    /// </summary>
    public const int MaxRequestBytes = 1_048_576;

    /// <summary>
    /// Current protocol version. Changing field types or semantics bumps this;
    /// adding optional fields does not.
    /// </summary>
    public const int Version = 2;
}
