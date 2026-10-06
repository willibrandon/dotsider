using NetFxBindingRedirects.Clr2.SharedDep;

namespace NetFxBindingRedirects.OldDep
{
    /// <summary>Forces a metadata reference to the signed SharedDep v1 fixture.</summary>
    public static class OldDepClass
    {
        /// <summary>Identifies which dependency version the CLR actually bound.</summary>
        public static string Marker() => SharedDepClass.Marker();
    }
}
