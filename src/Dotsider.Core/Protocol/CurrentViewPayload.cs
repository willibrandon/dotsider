namespace Dotsider.Core.Protocol;

/// <summary>
/// The current interactive view of a standard dotsider session.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record CurrentViewPayload(
    int Tab,
    string TabLabel,
    int PeSubTab,
    int DynamicSubTab,
    string AssemblyPath,
    int NavigationDepth,
    string? TracerState,
    bool HexIsDirty,
    bool HasEntryPoint,
    bool IsNativeAot,
    bool IsNetFramework);
