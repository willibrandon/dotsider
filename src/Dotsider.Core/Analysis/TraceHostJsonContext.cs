using System.Text.Json.Serialization;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Provides source-generated JSON metadata for trace-host messages.
/// Keeps the private transport contract out of the public diagnostics protocol.
/// Supplies Native AOT-safe serialization to both sides of the process boundary.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(TraceHostMessage))]
internal sealed partial class TraceHostJsonContext : JsonSerializerContext;
