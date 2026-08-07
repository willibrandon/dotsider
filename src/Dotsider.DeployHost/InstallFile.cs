using System.Text.Json.Serialization;

namespace Dotsider.DeployHost;

/// <summary>
/// Maps one embedded configuration resource to its fixed Linux destination.
/// Ownership and mode are applied only after candidate validation succeeds.
/// Destination validation confines privileged writes to known configuration areas.
/// </summary>
internal sealed record InstallFile(
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("destination")] string Destination,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("group")] string Group);
