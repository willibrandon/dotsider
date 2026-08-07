using System.Text.Json.Serialization;

namespace Dotsider.DeployHost;

/// <summary>
/// Describes the complete set of privileged files installed by the deploy host.
/// The manifest is embedded with its referenced assets in the Native AOT binary.
/// Schema validation prevents unexpected destinations or permission values.
/// </summary>
internal sealed record InstallManifest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("files")] InstallFile[] Files);
