using Newtonsoft.Json;

namespace NetFxBindingRedirects.OldDep
{
    /// <summary>Forces a metadata reference to Newtonsoft.Json 12.0.0.0.</summary>
    public static class OldDepClass
    {
        /// <summary>Round-trips a value through the v12 Newtonsoft serializer.</summary>
        public static string Serialize(object value) => JsonConvert.SerializeObject(value);
    }
}
