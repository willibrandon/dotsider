namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A single function imported from a native module.
/// </summary>
/// <param name="Name">The imported function name, or null for ordinal-only imports.</param>
/// <param name="Ordinal">The import ordinal, or null for named imports.</param>
/// <param name="Hint">The export-name-table hint for named imports, or null.</param>
public sealed record ImportedFunctionInfo(string? Name, ushort? Ordinal, ushort? Hint);
