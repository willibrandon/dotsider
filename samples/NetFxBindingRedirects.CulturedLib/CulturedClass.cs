using System.Globalization;
using System.Resources;

namespace NetFxBindingRedirects.CulturedLib
{
    /// <summary>Reads a culture-specific resource string from the satellite assembly.</summary>
    public static class CulturedClass
    {
        private static readonly ResourceManager Strings =
            new("NetFxBindingRedirects.CulturedLib.Strings", typeof(CulturedClass).Assembly);

        /// <summary>Returns the localized greeting for the supplied culture.</summary>
        public static string Greeting(CultureInfo culture) =>
            Strings.GetString("Greeting", culture) ?? string.Empty;
    }
}
