using Hex1b;
using ModelContextProtocol.Client;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Process-level tests for the dotsider-mcp CLI entry point.
/// </summary>
[TestClass]
public partial class McpCliTests
{
    private static readonly string s_projectPath = Path.Combine(
        FindRepoRoot(), "src", "Dotsider.Mcp");

    private static readonly string s_buildConfig = DetectBuildConfig();

    /// <summary>
    /// --help prints usage text identifying the binary and exits with success.
    /// </summary>
    [TestMethod]
    public async Task Help_ShowsUsageAndReturnsZero()
    {
        var (exitCode, stdout, _) = await RunMcpAsync("--help");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("dotsider-mcp", stdout);
        Assert.Contains("MCP", stdout);
    }

    /// <summary>
    /// --version emits a non-empty version string and a zero exit code.
    /// </summary>
    [TestMethod]
    public async Task Version_ReturnsZero()
    {
        var (exitCode, stdout, _) = await RunMcpAsync("--version");

        Assert.AreEqual(0, exitCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace(stdout));
    }

    /// <summary>
    /// Launched with no arguments, the CLI boots the MCP server and completes the client handshake.
    /// </summary>
    [TestMethod]
    public async Task NoArgs_StartsServerAndCompletesHandshake()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Launch the built DLL directly instead of going through `dotnet run`
        // which has project resolution overhead that can exceed the timeout.
        var dllPath = Path.Combine(
            FindRepoRoot(), "src", "Dotsider.Mcp", "bin", s_buildConfig, "net10.0", "dotsider-mcp.dll");
        Assert.IsTrue(File.Exists(dllPath), $"dotsider-mcp.dll not found: {dllPath}");

        await using var client = await McpClient.CreateAsync(
            new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = "dotnet",
                Arguments = [dllPath],
            }),
            cancellationToken: cts.Token);

        // If we get here, the MCP handshake completed successfully.
        // Verify the server reports its tools.
        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);
        Assert.IsNotEmpty(tools);
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

    /// <summary>
    /// Verifies the MCP server shuts down cleanly when Ctrl+C is pressed in a real terminal.
    /// Reproduces the parent-child process relationship: bash (session leader) → dotsider →
    /// dotsider-mcp. Without the fix, dotsider exits first, orphaning dotsider-mcp, and
    /// the orphaned process's read() on the terminal returns EIO → IOException.
    /// Regression test for https://github.com/willibrandon/dotsider/issues/108.
    /// </summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task CtrlC_InTerminal_ShutsDownWithoutTransportException()
    {
        var repoRoot = FindRepoRoot();
        var dotsiderExe = Path.Combine(
            repoRoot, "src", "Dotsider", "bin", s_buildConfig, "net10.0", "dotsider");
        var mcpDir = Path.Combine(
            repoRoot, "src", "Dotsider.Mcp", "bin", s_buildConfig, "net10.0");

        Assert.IsTrue(File.Exists(dotsiderExe), $"dotsider not found: {dotsiderExe}");
        Assert.IsTrue(File.Exists(Path.Combine(mcpDir, "dotsider-mcp")),
            $"dotsider-mcp not found in: {mcpDir}");

        // Start an interactive shell in the PTY (bash becomes session leader).
        // This mirrors the real user experience: shell → dotsider → dotsider-mcp.
        var env = new Dictionary<string, string>
        {
            ["PATH"] = $"{mcpDir}:{Environment.GetEnvironmentVariable("PATH")}"
        };

        await using var pty = new Hex1bTerminalChildProcess(
            "/bin/bash", ["--norc", "--noprofile"],
            environment: env,
            initialWidth: 160, initialHeight: 24);

        var ct = CancellationToken.None;
        await pty.StartAsync(ct);

        var output = new StringBuilder();
        var serverStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shellReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ctrlCSent = false;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeoutCts.Token;

        var readTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                var chunk = await pty.ReadOutputAsync(token);
                if (chunk.IsEmpty)
                    break;

                var text = Encoding.UTF8.GetString(chunk.Span);
                output.Append(text);

                if (text.Contains("Application started"))
                    serverStarted.TrySetResult();

                // Only eligible after Ctrl+C has been sent — a READY> prompt
                // proves dotsider + dotsider-mcp have both exited and bash
                // regained the foreground. If shutdown is deadlocked this never fires.
                if (Volatile.Read(ref ctrlCSent) && text.Contains("READY>"))
                    shellReady.TrySetResult();
            }
        }, token);

        // Set a unique prompt we can detect after Ctrl+C
        await pty.WriteInputAsync(Encoding.UTF8.GetBytes("PS1='READY> '\n"), token);
        await Task.Delay(500, token);

        // Launch dotsider agent mcp from bash (creates the parent-child relationship)
        await pty.WriteInputAsync(
            Encoding.UTF8.GetBytes($"{dotsiderExe} agent mcp\n"), token);

        await serverStarted.Task.WaitAsync(token);

        // Arm the prompt detector, then send Ctrl+C.
        Volatile.Write(ref ctrlCSent, true);
        await pty.WriteInputAsync(new byte[] { 0x03 }, token);

        // Wait for bash to regain the foreground (proves shutdown completed).
        // If shutdown is deadlocked, this times out and the test FAILS.
        await shellReady.Task.WaitAsync(token);

        // Exit bash cleanly — must succeed, not silently swallow a timeout.
        await pty.WriteInputAsync(Encoding.UTF8.GetBytes("exit\n"), token);
        await pty.WaitForExitAsync(token);

        await timeoutCts.CancelAsync();
        try { await readTask; }
        catch (OperationCanceledException) { }

        var allOutput = AnsiEscapeRegex().Replace(output.ToString(), "");

        Assert.DoesNotContain("IOException", allOutput);
        Assert.DoesNotContain("Input/output error", allOutput);
        Assert.DoesNotContain("reading failed", allOutput);
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

    [GeneratedRegex(@"\e\[[^@-~]*[@-~]")]
    private static partial Regex AnsiEscapeRegex();
}
