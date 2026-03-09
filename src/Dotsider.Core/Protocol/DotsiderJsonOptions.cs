using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Shared JSON serialization options for the dotsider diagnostics protocol.
/// </summary>
public static class DotsiderJsonOptions
{
    /// <summary>camelCase, ignore nulls, case-insensitive reads.</summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
