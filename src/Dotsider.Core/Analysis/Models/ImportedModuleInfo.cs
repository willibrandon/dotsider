namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A native module referenced by the PE import table, with the functions imported from it.
/// </summary>
/// <param name="ModuleName">The module file name (e.g. "KERNEL32.dll").</param>
/// <param name="Functions">The functions imported from the module.</param>
public sealed record ImportedModuleInfo(
    string ModuleName, IReadOnlyList<ImportedFunctionInfo> Functions);
