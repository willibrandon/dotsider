namespace Dotsider.Infrastructure;

/// <summary>
/// Discovers running dotsider TUI instances by scanning Unix domain socket files.
/// </summary>
internal sealed class SessionDiscovery
{
    private static string SocketDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".dotsider", "sockets");

    /// <summary>
    /// Returns the dotsider socket path for a given PID.
    /// </summary>
    public static string GetDotsiderSocketPath(int pid) =>
        Path.Combine(SocketDir, $"{pid}.dotsider.socket");

    /// <summary>
    /// Returns the hex1b diagnostics socket path for a given PID.
    /// </summary>
    public static string GetHex1bSocketPath(int pid) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hex1b", "sockets", $"{pid}.diagnostics.socket");

    /// <summary>
    /// Scans for all dotsider socket files and returns discovered sessions.
    /// </summary>
    public IReadOnlyList<DiscoveredSession> Scan()
    {
        var dir = SocketDir;
        if (!Directory.Exists(dir))
            return [];

        var sessions = new List<DiscoveredSession>();
        foreach (var file in Directory.GetFiles(dir, "*.dotsider.socket"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var pidStr = name.Replace(".dotsider", "");
            if (int.TryParse(pidStr, out var pid))
            {
                sessions.Add(new DiscoveredSession(pid, file));
            }
        }

        return sessions;
    }
}
