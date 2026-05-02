namespace NetFxBindingRedirects.Clr2.PrivatePathLib
{
    /// <summary>Lives under bin/.../lib/ and is reached only via the probing privatePath.</summary>
    public static class PrivatePathClass
    {
        /// <summary>Returns a marker string proving the assembly was loaded.</summary>
        public static string Marker() => "Clr2.PrivatePathLib loaded from probing privatePath";
    }
}
