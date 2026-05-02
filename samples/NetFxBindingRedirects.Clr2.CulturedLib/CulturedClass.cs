using System.Globalization;
using System.Resources;

namespace NetFxBindingRedirects.Clr2.CulturedLib
{
    /// <summary>Reads a culture-specific resource string from the satellite assembly.</summary>
    public static class CulturedClass
    {
        private static readonly ResourceManager Strings =
            new("NetFxBindingRedirects.Clr2.CulturedLib.Strings", typeof(CulturedClass).Assembly);

        /// <summary>Returns the localized greeting for the supplied culture.</summary>
        public static string Greeting(CultureInfo culture) =>
            Strings.GetString("Greeting", culture) ?? string.Empty;
    }
}
