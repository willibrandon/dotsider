namespace NetFxBindingRedirects.Clr2.SharedDep
{
    /// <summary>
    /// The same-name/same-key SharedDep identity, built here at <c>AssemblyVersion 1.0.0.0</c>
    /// and again at <c>2.0.0.0</c> in the V2 sibling project. Both builds emit a DLL named
    /// <c>NetFxBindingRedirects.Clr2.SharedDep.dll</c>; the root EXE's <c>bindingRedirect</c>
    /// collapses requests for v1 onto the v2 build that ships app-local.
    /// </summary>
    public static class SharedDepClass
    {
        /// <summary>Identifies which build of <c>SharedDep</c> the loader actually picked.</summary>
        public static string Marker() => "SharedDep v1.0.0.0";
    }
}
