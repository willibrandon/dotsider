using NetFxBindingRedirects.Clr2.SharedDep;

namespace NetFxBindingRedirects.NewDep
{
    /// <summary>Forces a metadata reference to the signed SharedDep v2 fixture.</summary>
    public static class NewDepClass
    {
        /// <summary>Identifies which dependency version the CLR actually bound.</summary>
        public static string Marker() => SharedDepClass.Marker();
    }
}
