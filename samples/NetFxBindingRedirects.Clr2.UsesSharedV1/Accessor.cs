using System.Reflection;

namespace NetFxBindingRedirects.Clr2.UsesSharedV1
{
    /// <summary>
    /// Forces a transitive bind to <c>SharedDep</c> through this assembly's metadata reference
    /// (recorded as <c>SharedDep, Version=1.0.0.0</c>). The runtime oracle calls
    /// <see cref="GetSharedAssembly"/> and captures the resulting <see cref="Assembly"/> so
    /// the dotsider binder can be compared against the live <c>bindingRedirect</c>-applied
    /// resolution rather than a standalone <c>Assembly.Load</c>.
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
