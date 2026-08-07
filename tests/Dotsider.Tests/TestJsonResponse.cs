using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotsider.Tests;

/// <summary>
/// Serializes test-only anonymous payloads into protocol elements.
/// Lets protocol fixtures exercise arbitrary input shapes without production metadata.
/// Keeps reflection use isolated to the framework-dependent test assembly.
/// </summary>
internal static class TestJsonResponse
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static JsonElement Element<T>(T value) =>
        JsonSerializer.SerializeToElement(value, Options);
}
