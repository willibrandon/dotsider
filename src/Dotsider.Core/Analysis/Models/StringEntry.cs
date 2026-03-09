namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A string extracted from the assembly, along with its source and offset.
/// </summary>
/// <param name="Offset">The byte offset or heap handle where the string was found.</param>
/// <param name="Value">The string content.</param>
/// <param name="Source">Which string source this entry came from.</param>
public sealed record StringEntry(
    int Offset,
    string Value,
    StringSource Source);
