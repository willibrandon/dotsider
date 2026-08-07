using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Dotsider.Core.Protocol;

/// <summary>
/// JSON response from a dotsider diagnostics socket.
/// </summary>
public sealed class DotsiderResponse
{
    /// <summary>Protocol version echoed in every response.</summary>
    [JsonRequired]
    public int V { get; set; } = DotsiderProtocol.Version;

    /// <summary>Whether the request succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Error message if the request failed.</summary>
    public string? Error { get; set; }

    /// <summary>Response payload.</summary>
    public JsonElement? Data { get; set; }

    /// <summary>Creates a successful response without a payload.</summary>
    public static DotsiderResponse Ok() => new() { Success = true };

    /// <summary>Creates a successful response from an existing JSON payload.</summary>
    public static DotsiderResponse Ok(JsonElement? data) => new() { Success = true, Data = data };

    /// <summary>Creates a successful response using the protocol's source-generated metadata.</summary>
    public static DotsiderResponse Ok<T>(T data)
    {
        if (data is null)
        {
            return Ok();
        }

        var jsonTypeInfo = (DotsiderJsonContext.Protocol.GetTypeInfo(data.GetType())
            ?? DotsiderJsonContext.Protocol.GetTypeInfo(typeof(T))) ?? throw new InvalidOperationException(
                $"No source-generated JSON metadata is registered for {data.GetType()}.");
        return new DotsiderResponse
        {
            Success = true,
            Data = JsonSerializer.SerializeToElement(data, jsonTypeInfo)
        };
    }

    /// <summary>Creates a successful response using source-generated JSON metadata.</summary>
    public static DotsiderResponse Ok<T>(T data, JsonTypeInfo<T> jsonTypeInfo) => new()
    {
        Success = true,
        Data = data is null ? null : JsonSerializer.SerializeToElement(data, jsonTypeInfo)
    };

    /// <summary>Creates an error response with the given message.</summary>
    public static DotsiderResponse Fail(string error) => new() { Success = false, Error = error };

}
