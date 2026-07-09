using System.Diagnostics;

namespace Dotsider.Core.Analysis.ReadyToRun;

internal static class ReadyToRunDiagnostics
{
    private const string EnabledVariable = "DOTSIDER_R2R_DIAGNOSTICS";
    private const string PathVariable = "DOTSIDER_R2R_DIAGNOSTICS_PATH";
    private const string MaxLinesVariable = "DOTSIDER_R2R_DIAGNOSTICS_MAX_LINES";
    private const int DefaultMaxLines = 50_000;

    private static readonly Lock Gate = new();
    private static readonly bool Enabled = IsEnabled(Environment.GetEnvironmentVariable(EnabledVariable));
    private static readonly string? LogPath = Enabled ? NormalizePath(Environment.GetEnvironmentVariable(PathVariable)) : null;
    private static readonly int MaxLines = ResolveMaxLines(Environment.GetEnvironmentVariable(MaxLinesVariable));
    private static readonly Lazy<StreamWriter?> Writer = new(CreateWriter);
    private static int s_linesWritten;
    private static int s_truncated;

    public static void Write(string message)
    {
        if (!Enabled)
            return;

        if (MaxLines > 0)
        {
            var lineNumber = Interlocked.Increment(ref s_linesWritten);
            if (lineNumber > MaxLines)
            {
                if (Interlocked.Exchange(ref s_truncated, 1) == 0)
                    WriteCore($"diagnostics-truncated max-lines={MaxLines}");
                return;
            }
        }

        WriteCore(message);
    }

    private static void WriteCore(string message)
    {
        var line = $"{DateTimeOffset.UtcNow:O} pid={Environment.ProcessId} tid={Environment.CurrentManagedThreadId} {message}";
        try
        {
            if (LogPath is { Length: > 0 })
            {
                lock (Gate)
                {
                    Writer.Value?.WriteLine(line);
                }
            }
            else
            {
                Trace.WriteLine(line);
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

    private static int ResolveMaxLines(string? value) =>
        int.TryParse(value, out var maxLines) && maxLines >= 0 ? maxLines : DefaultMaxLines;

    private static StreamWriter? CreateWriter()
    {
        if (LogPath is not { Length: > 0 })
            return null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? ".");
            var writer = new StreamWriter(new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };
            writer.WriteLine($"{DateTimeOffset.UtcNow:O} pid={Environment.ProcessId} diagnostics-start");
            return writer;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
