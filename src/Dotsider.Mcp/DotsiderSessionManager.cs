namespace Dotsider.Mcp;

/// <summary>
/// Discovers and manages connections to running dotsider TUI instances.
/// Registered as a singleton in the MCP server's DI container.
/// </summary>
public sealed class DotsiderSessionManager(string socketDir)
{
    private static readonly string s_defaultSocketDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".dotsider", "sockets");

    /// <summary>
    /// Creates a session manager using the default socket directory (~/.dotsider/sockets).
    /// </summary>
    public DotsiderSessionManager() : this(s_defaultSocketDir) { }

    /// <summary>
    /// Scans for all running dotsider instances and returns their PIDs and socket paths.
    /// </summary>
    public IReadOnlyList<(int Pid, string SocketPath)> DiscoverSessions()
    {
        if (!Directory.Exists(socketDir))
            return [];

        var sessions = new List<(int, string)>();
        foreach (var file in Directory.GetFiles(socketDir, "*.dotsider.socket"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var pidStr = name.Replace(".dotsider", "");
            if (int.TryParse(pidStr, out var pid))
                sessions.Add((pid, file));
        }

        return sessions;
    }

    /// <summary>
    /// Gets a client for communicating with the specified dotsider instance.
    /// </summary>
    public RemoteDotsiderTarget GetTarget(int pid)
    {
        var socketPath = Path.Combine(socketDir, $"{pid}.dotsider.socket");
        return new RemoteDotsiderTarget(socketPath);
    }

    /// <summary>
    /// Gets the hex1b diagnostics socket path for capture/input operations.
    /// </summary>
    public static string GetHex1bSocketPath(int pid) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hex1b", "sockets", $"{pid}.diagnostics.socket");
}
