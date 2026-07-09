using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Diagnostics;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end tests that start a real headless TUI with the production-equivalent
/// manual terminal setup (McpDiagnosticsPresentationFilter), then exercise the
/// hex1b diagnostics socket for sessions capture and screen capture workflows.
/// </summary>
[TestClass]
public class SessionDiagnosticsSocketTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Starts a headless TUI mirroring the production Program.cs setup:
    /// manual Hex1bTerminal with McpDiagnosticsPresentationFilter.
    /// Returns the diagnostics socket path and a CTS to stop the app.
    /// </summary>
    private static async Task<(Hex1bTerminal terminal, Hex1bApp app, McpDiagnosticsPresentationFilter filter,
        DotsiderDiagnosticsListener listener, DotsiderState state, Task runTask, CancellationTokenSource cts)>
        StartTuiAsync()
    {
        var pendingMutations = new ConcurrentQueue<Action<DotsiderState>>();

        var workload = new Hex1bAppWorkloadAdapter();
        var socketId = TestSocketIds.NextPid();
        var filter = CreateDiagnosticsFilter(socketId);

        var terminalOptions = new Hex1bTerminalOptions
        {
            PresentationAdapter = new HeadlessPresentationAdapter(120, 30),
            WorkloadAdapter = workload
        };
        terminalOptions.PresentationFilters.Add(filter);
        var terminal = new Hex1bTerminal(terminalOptions);

        DotsiderState? state = null;
        Hex1bApp? app = null;

        app = new Hex1bApp(
            ctx =>
            {
                state ??= new DotsiderState(app!, Samples.HelloWorldDll, pendingMutations);
                var dotsiderApp = new DotsiderApp(state);
                return dotsiderApp.Build(ctx);
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                Theme = DotsiderTheme.Create(),
                EnableInputCoalescing = false
            });

        var listener = new DotsiderDiagnosticsListener(() => state);
        listener.StartListening(overridePid: socketId);

        // Start the app and wait for state initialization + first render
        var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);

        await TestHelpers.WaitUntilAsync(
            () => state is not null,
            TimeSpan.FromSeconds(10));

        // Wait for the screen to contain rendered content (not just blank spaces).
        // The General tab renders "HelloWorld" in the title bar on first frame.
        await TestHelpers.WaitUntilAsync(
            () =>
            {
                using var snapshot = terminal.CreateSnapshot();
                return snapshot.GetScreenText().Contains("HelloWorld");
            },
            TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(50));

        await TestHelpers.WaitUntilAsync(
            () => CanConnectToSocket(filter.SocketPath),
            TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(50));

        return (terminal, app, filter, listener, state!, runTask, cts);
    }

    private static McpDiagnosticsPresentationFilter CreateDiagnosticsFilter(int socketId)
    {
        var filter = new McpDiagnosticsPresentationFilter($"dotsider-test-{socketId}");
        var socketDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".hex1b",
            "sockets");
        var socketPath = Path.Combine(socketDirectory, $"{socketId}.diagnostics.socket");

        typeof(McpDiagnosticsPresentationFilter)
            .GetField("_socketPath", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(filter, socketPath);

        return filter;
    }

    private static bool CanConnectToSocket(string socketPath)
    {
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixDomainSocketEndPoint(socketPath));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>
    /// Stops the app cleanly and disposes all resources.
    /// </summary>
    private static async Task StopAndDisposeAsync(
        Hex1bTerminal terminal, Hex1bApp app,
        McpDiagnosticsPresentationFilter filter,
        DotsiderDiagnosticsListener listener,
        DotsiderState state, Task runTask, CancellationTokenSource cts)
    {
        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }

        await listener.DisposeAsync();
        state.Dispose();
        app.Dispose();
        await filter.DisposeAsync();
        await terminal.DisposeAsync();
        cts.Dispose();
    }

    /// <summary>
    /// Verifies diagnostics socket exists after tui start.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DiagnosticsSocket_ExistsAfterTuiStart()
    {
        var (terminal, app, filter, listener, state, runTask, cts) = await StartTuiAsync();
        try
        {
            Assert.IsTrue(File.Exists(filter.SocketPath),
                $"Hex1b diagnostics socket was not created at {filter.SocketPath}");
        }
        finally
        {
            await StopAndDisposeAsync(terminal, app, filter, listener, state, runTask, cts);
        }
    }

    /// <summary>
    /// Verifies diagnostics socket removed after dispose.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DiagnosticsSocket_RemovedAfterDispose()
    {
        var (terminal, app, filter, listener, state, runTask, cts) = await StartTuiAsync();
        var socketPath = filter.SocketPath;

        await StopAndDisposeAsync(terminal, app, filter, listener, state, runTask, cts);

        Assert.IsFalse(File.Exists(socketPath),
            $"Hex1b diagnostics socket was not cleaned up at {socketPath}");
    }

    /// <summary>
    /// Verifies sessions capture returns screen content via real socket.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SessionsCapture_ReturnsScreenContent_ViaRealSocket()
    {
        var (terminal, app, filter, listener, state, runTask, cts) = await StartTuiAsync();
        try
        {
            // Send the same capture request that `dotsider sessions capture <pid>` uses
            var requestJson = JsonSerializer.Serialize(
                new { method = "capture", format = "text" }, DotsiderJsonOptions.Default);

            var responseJson = await DotsiderClient.SendRawAsync(filter.SocketPath, requestJson, CancellationToken.None);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            Assert.IsTrue(response.GetProperty("success").GetBoolean(), "Capture request failed");
            Assert.IsTrue(response.TryGetProperty("data", out var data), "Capture response missing data");

            var content = data.GetString()!;
            Assert.IsNotEmpty(content);
            Assert.Contains("dotsider", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await StopAndDisposeAsync(terminal, app, filter, listener, state, runTask, cts);
        }
    }

    /// <summary>
    /// Verifies capture screen returns assembly content via real socket.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CaptureScreen_ReturnsAssemblyContent_ViaRealSocket()
    {
        var (terminal, app, filter, listener, state, runTask, cts) = await StartTuiAsync();
        try
        {
            // Exercise the same code path that MCP capture_screen uses
            var requestJson = JsonSerializer.Serialize(
                new { method = "capture", format = "text" }, DotsiderJsonOptions.Default);

            var responseJson = await DotsiderClient.SendRawAsync(filter.SocketPath, requestJson, CancellationToken.None);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            Assert.IsTrue(response.GetProperty("success").GetBoolean());
            var content = response.GetProperty("data").GetString()!;

            // The General tab shows assembly info — verify real assembly data appears
            Assert.Contains("HelloWorld", content);
        }
        finally
        {
            await StopAndDisposeAsync(terminal, app, filter, listener, state, runTask, cts);
        }
    }
}
