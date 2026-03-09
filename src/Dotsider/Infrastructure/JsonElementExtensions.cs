using System.Text.Json;

namespace Dotsider.Infrastructure;

/// <summary>
/// Extension methods for safely accessing JsonElement properties.
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>
    /// Returns the property value if it exists, or null.
    /// </summary>
    public static JsonElement? GetPropertyOrNull(this JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) ? value : null;

    /// <summary>
    /// Returns a display string for a JsonElement regardless of its value kind
    /// (handles both String and Number values safely).
    /// </summary>
    public static string GetDisplayString(this JsonElement element, string fallback = "")
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? fallback,
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => fallback,
            _ => element.ToString()
        };
}
