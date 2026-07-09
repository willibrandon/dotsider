using System.Diagnostics;

namespace Dotsider.Core.Analysis.ReadyToRun;

internal static class ReadyToRunDiagnostics
{
    private const string EnabledVariable = "DOTSIDER_R2R_DIAGNOSTICS";
    private const string PathVariable = "DOTSIDER_R2R_DIAGNOSTICS_PATH";

    private static readonly Lock Gate = new();
    private static readonly bool Enabled = IsEnabled(Environment.GetEnvironmentVariable(EnabledVariable));
    private static readonly string? LogPath = Enabled ? NormalizePath(Environment.GetEnvironmentVariable(PathVariable)) : null;

    public static void Write(string message)
    {
        if (!Enabled)
            return;

        var line = $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}";
        try
        {
            if (LogPath is { Length: > 0 })
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? ".");
                    File.AppendAllText(LogPath, line);
                }
            }
            else
            {
                Trace.Write(line);
            }
        }
        catch (Exception)
        {
            // Diagnostics must never change ReadyToRun parsing behavior.
        }
    }

    private static bool IsEnabled(string? value) =>
        value is not null
        && !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizePath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
}
