using System.Diagnostics;
using ModelContextProtocol.Client;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Process-level tests for the dotsider-mcp CLI entry point.
/// </summary>
public class McpCliTests
{
    private static readonly string s_projectPath = Path.Combine(
        FindRepoRoot(), "src", "Dotsider.Mcp");

    private static readonly string s_buildConfig = DetectBuildConfig();

    [Fact]
    public async Task Help_ShowsUsageAndReturnsZero()
    {
        var (exitCode, stdout, _) = await RunMcpAsync("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("dotsider-mcp", stdout);
        Assert.Contains("MCP", stdout);
    }

    [Fact]
    public async Task Version_ReturnsZero()
    {
        var (exitCode, stdout, _) = await RunMcpAsync("--version");

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdout));
    }

    [Fact]
    public async Task NoArgs_StartsServerAndCompletesHandshake()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await using var client = await McpClient.CreateAsync(
            new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = "dotnet",
                Arguments = [$"run", "--no-build", "-c", s_buildConfig, "--project", s_projectPath],
            }),
            cancellationToken: cts.Token);

        // If we get here, the MCP handshake completed successfully.
        // Verify the server reports its tools.
        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);
        Assert.NotEmpty(tools);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunMcpAsync(
        params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build -c {s_buildConfig} --project \"{s_projectPath}\" -- {string.Join(' ', arguments)}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout, stderr);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, ".git")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not find repo root");
    }

    private static string DetectBuildConfig()
    {
        // BaseDirectory is e.g. .../bin/MyConfig/net10.0/ — extract the config segment
        var parts = AppContext.BaseDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("bin", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }

        return "Debug";
    }
}
