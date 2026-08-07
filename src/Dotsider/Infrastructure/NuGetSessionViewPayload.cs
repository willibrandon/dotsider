namespace Dotsider.Infrastructure;

/// <summary>
/// NuGet browsing state exposed by a live session.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record NuGetSessionViewPayload(
    string Mode,
    bool IsBrowsingPackage,
    int? Tab,
    string? SelectedDll);
