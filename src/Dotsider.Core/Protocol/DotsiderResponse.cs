using System.Text.Json.Serialization;

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

    /// <summary>Response payload, serialized as the appropriate type.</summary>
    public object? Data { get; set; }

    /// <summary>Creates a successful response with the given data.</summary>
    public static DotsiderResponse Ok(object? data = null) => new() { Success = true, Data = data };

    /// <summary>Creates an error response with the given message.</summary>
    public static DotsiderResponse Fail(string error) => new() { Success = false, Error = error };
}
