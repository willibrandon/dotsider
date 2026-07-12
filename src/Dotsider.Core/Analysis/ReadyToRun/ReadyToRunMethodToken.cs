using System.Reflection.Metadata;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Constructs ReadyToRun MethodDef and MemberRef tokens after validating their encoded row against
/// both the ECMA-335 token width and the active metadata scope when one is available.
/// </summary>
internal static class ReadyToRunMethodToken
{
    /// <summary>Validates <paramref name="rid"/> and returns its exact entity token.</summary>
    /// <param name="rid">The one-based metadata row encoded in the ReadyToRun signature.</param>
    /// <param name="kind">The MethodDef or MemberRef table selected by the signature flags.</param>
    /// <param name="metadata">The active metadata scope, or null when that module is unavailable.</param>
    /// <returns>The validated MethodDef or MemberRef entity token.</returns>
    /// <exception cref="BadImageFormatException">
    /// The row is nil, exceeds the 24-bit token width, or is outside the available metadata table.
    /// </exception>
    internal static int Create(uint rid, HandleKind kind, MetadataReader? metadata)
    {
        var rowCount = kind switch
        {
            HandleKind.MemberReference => metadata?.MemberReferences.Count,
            HandleKind.MethodDefinition => metadata?.MethodDefinitions.Count,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        if (rid is 0 or > 0x00FF_FFFF || rowCount is { } count && rid > count)
        {
            var tableSize = rowCount is { } size ? $"; table size is {size}" : "";
            throw new BadImageFormatException(
                $"ReadyToRun signature {kind} row {rid} is invalid{tableSize}.");
        }

        var tokenType = kind == HandleKind.MemberReference ? 0x0A00_0000 : 0x0600_0000;
        return tokenType | (int)rid;
    }
}
