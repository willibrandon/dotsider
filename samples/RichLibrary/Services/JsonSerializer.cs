using System.Text.Json;
using Newtonsoft.Json;
using RichLibrary.Models;

namespace RichLibrary.Services;

/// <summary>
/// Demonstrates usage of Newtonsoft.Json and System.Text.Json for serialization.
/// Creates rich assembly references and user strings for dotsider analysis.
/// </summary>
public static class JsonSerializer
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Serializes a user to JSON using Newtonsoft.Json.</summary>
    public static string SerializeWithNewtonsoft(User user)
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
        return JsonConvert.SerializeObject(user, settings);
    }

    /// <summary>Deserializes a user from JSON using Newtonsoft.Json.</summary>
    public static User? DeserializeWithNewtonsoft(string json)
    {
        return JsonConvert.DeserializeObject<User>(json);
    }

    /// <summary>Serializes a user to JSON using System.Text.Json.</summary>
    public static string SerializeWithSystemTextJson(User user)
    {
        return System.Text.Json.JsonSerializer.Serialize(user, s_options);
    }

    /// <summary>Deserializes a user from JSON using System.Text.Json.</summary>
    public static User? DeserializeWithSystemTextJson(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<User>(json);
    }
}
