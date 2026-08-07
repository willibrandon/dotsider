using Dotsider.Core.Protocol;
using System.Text.Json;

namespace Dotsider.Mcp;

/// <summary>
/// Serializes MCP results with the application's source-generated JSON metadata.
/// Resolves runtime result types through the explicit MCP serialization context.
/// Fails clearly when an unregistered response would require reflection.
/// </summary>
internal static class McpJson
{
    public static string Serialize<T>(T value)
    {
        if (value is null)
        {
            return "null";
        }

        var jsonTypeInfo = McpJsonContext.Application.GetTypeInfo(value.GetType())
            ?? DotsiderJsonContext.Protocol.GetTypeInfo(value.GetType())
            ?? McpJsonContext.Application.GetTypeInfo(typeof(T))
            ?? DotsiderJsonContext.Protocol.GetTypeInfo(typeof(T))
            ?? throw new InvalidOperationException(
                $"No source-generated JSON metadata is registered for {value.GetType()}.");
        return JsonSerializer.Serialize(value, jsonTypeInfo);
    }
}
