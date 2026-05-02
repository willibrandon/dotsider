namespace NetFxBindingRedirects.Clr2.CodeBaseLib
{
    /// <summary>Lives under bin/.../external/ and is reached only via &lt;codeBase href&gt; in app.config.</summary>
    public static class CodeBaseClass
    {
        /// <summary>Returns a marker string proving the codeBase href was honored.</summary>
        public static string Marker() => "Clr2.CodeBaseLib loaded from configured codeBase href";
    }
}
