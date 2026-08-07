namespace Dotsider.Infrastructure;

/// <summary>
/// NuGet package details exposed by a live session.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record NuGetSessionAssemblyPayload(
    string Mode,
    string FilePath,
    string FileName,
    string? PackageId,
    string? PackageVersion,
    string? Authors,
    string? Description,
    int DllCount,
    string? SelectedDll);
