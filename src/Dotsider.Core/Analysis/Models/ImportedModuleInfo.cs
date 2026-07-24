namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A module referenced by a PE, ELF, Mach-O, or WebAssembly import table, with the
/// functions imported from it.
/// </summary>
/// <param name="ModuleName">
/// The module name, such as <c>KERNEL32.dll</c>, <c>libc.so.6</c>, or
/// <c>(unversioned)</c> for an ELF symbol without safe library attribution.
/// </param>
/// <param name="Functions">The functions imported from the module.</param>
public sealed record ImportedModuleInfo(
    string ModuleName, IReadOnlyList<ImportedFunctionInfo> Functions);
