using Newtonsoft.Json;

namespace NetFxBindingRedirects.NewDep
{
    /// <summary>Forces a metadata reference to Newtonsoft.Json 13.0.0.0.</summary>
    public static class NewDepClass
    {
        /// <summary>Round-trips a value through the v13 Newtonsoft serializer.</summary>
        public static T? Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json);
    }
}
