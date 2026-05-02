using System.Reflection;

namespace NetFxBindingRedirects.Clr2.UsesSharedV2
{
    /// <summary>
    /// Forces a transitive bind to <c>SharedDep</c> through this assembly's metadata reference
    /// (recorded as <c>SharedDep, Version=2.0.0.0</c>). Mirror of the V1 sibling: the runtime
    /// oracle captures both call sites and asserts they collapse onto the same loaded assembly.
    /// </summary>
    public static class Accessor
    {
        /// <summary>Returns the <see cref="Assembly"/> the loader actually picked for <c>SharedDep</c>.</summary>
        public static Assembly GetSharedAssembly() =>
            typeof(SharedDep.SharedDepClass).Assembly;

        /// <summary>Returns the marker string from whichever build of <c>SharedDep</c> was loaded.</summary>
        public static string GetMarker() => SharedDep.SharedDepClass.Marker();
    }
}
