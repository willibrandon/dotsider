using System.Text.Json.Serialization;

namespace Dotsider.DeployHost;

/// <summary>
/// Supplies compile-time JSON metadata required by the Native AOT deploy host.
/// Only the bounded installation manifest participates in serialization.
/// Reflection-based JSON contracts are intentionally unavailable.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(InstallManifest))]
internal sealed partial class DeployHostJsonContext : JsonSerializerContext;
