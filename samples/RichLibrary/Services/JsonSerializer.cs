using Newtonsoft.Json;
using RichLibrary.Models;

namespace RichLibrary.Services;

/// <summary>
/// Demonstrates usage of Newtonsoft.Json and System.Text.Json for serialization.
/// Creates rich assembly references and user strings for dotsider analysis.
/// </summary>
public static class JsonSerializer
{
    public static string SerializeWithNewtonsoft(User user)
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
        return JsonConvert.SerializeObject(user, settings);
    }

    public static User? DeserializeWithNewtonsoft(string json)
    {
        return JsonConvert.DeserializeObject<User>(json);
    }

    public static string SerializeWithSystemTextJson(User user)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        return System.Text.Json.JsonSerializer.Serialize(user, options);
    }

    public static User? DeserializeWithSystemTextJson(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<User>(json);
    }
}
