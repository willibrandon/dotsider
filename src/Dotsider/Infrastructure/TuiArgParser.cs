namespace Dotsider.Infrastructure;

/// <summary>
/// Parses TUI-mode command-line arguments (file path, --tab, --min-len)
/// independent of argument ordering.
/// </summary>
internal sealed class TuiArgParser
{
    /// <summary>The resolved assembly file path.</summary>
    public string FilePath { get; private set; } = "";

    /// <summary>The initial tab index (0-based, from --tab which is 1-based).</summary>
    public int InitialTab { get; private set; }

    /// <summary>The minimum string length for raw string extraction.</summary>
    public int MinStringLength { get; private set; } = 4;

    /// <summary>The escape key timeout in milliseconds.</summary>
    public int EscapeTimeoutMs { get; private set; } = 100;

    /// <summary>
    /// Parses TUI arguments given the already-resolved file path.
    /// Options can appear before or after the file path.
    /// </summary>
    public static TuiArgParser Parse(string[] args, string filePath)
    {
        var result = new TuiArgParser { FilePath = filePath };

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == filePath)
                continue;

            if (args[i] is "--tab" or "-t" && i + 1 < args.Length
                && int.TryParse(args[i + 1], out var tab))
            {
                result.InitialTab = Math.Clamp(tab - 1, 0, 7);
                i++;
            }
            else if (args[i] is "--min-len" or "-n" && i + 1 < args.Length
                && int.TryParse(args[i + 1], out var minLen))
            {
                result.MinStringLength = minLen;
                i++;
            }
            else if (args[i] is "--escape-timeout" or "-e" && i + 1 < args.Length
                && int.TryParse(args[i + 1], out var escTimeout))
            {
                result.EscapeTimeoutMs = Math.Max(10, escTimeout);
                i++;
            }
        }

        return result;
    }
}
