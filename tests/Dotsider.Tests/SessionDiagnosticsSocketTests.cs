using System.Collections.Concurrent;
using System.Text.Json;
using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Diagnostics;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end tests that start a real headless TUI with the production-equivalent
/// manual terminal setup (McpDiagnosticsPresentationFilter), then exercise the
/// hex1b diagnostics socket for sessions capture and screen capture workflows.
/// </summary>
[Collection("SampleAssemblies")]
public class SessionDiagnosticsSocketTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Starts a headless TUI mirroring the production Program.cs setup:
    /// manual Hex1bTerminal with McpDiagnosticsPresentationFilter.
    /// Returns the diagnostics socket path and a CTS to stop the app.
    /// </summary>
    private async Task<(Hex1bTerminal terminal, Hex1bApp app, McpDiagnosticsPresentationFilter filter,
        DotsiderDiagnosticsListener listener, DotsiderState state, Task runTask, CancellationTokenSource cts)>
        StartTuiAsync()
    {
        var pendingMutations = new ConcurrentQueue<Action<DotsiderState>>();

        var workload = new Hex1bAppWorkloadAdapter();
        var filter = new McpDiagnosticsPresentationFilter("dotsider-test");

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
                state ??= new DotsiderState(app!, samples.HelloWorldDll, pendingMutations);
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
        listener.StartListening();

        // Start the app and allow time for first render
        var cts = new CancellationTokenSource();
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(50);

        // Wait for the TUI state to be initialized and rendered
        await TestHelpers.WaitUntilAsync(
            () => state is not null,
            TimeSpan.FromSeconds(10));
        await Task.Delay(50);

        return (terminal, app, filter, listener, state!, runTask, cts);
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

    [Fact(Timeout = 30_000)]
    public async Task DiagnosticsSocket_ExistsAfterTuiStart()
    {
        var (terminal, app, filter, listener, state, runTask, cts) = await StartTuiAsync();
        try
        {
            Assert.True(File.Exists(filter.SocketPath),
                $"Hex1b diagnostics socket was not created at {filter.SocketPath}");
        }
        finally
        {
            await StopAndDisposeAsync(terminal, app, filter, listener, state, runTask, cts);
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task DiagnosticsSocket_RemovedAfterDispose()
    {
        var (terminal, app, filter, listener, state, runTask, cts) = await StartTuiAsync();
        var socketPath = filter.SocketPath;

        await StopAndDisposeAsync(terminal, app, filter, listener, state, runTask, cts);

        Assert.False(File.Exists(socketPath),
            $"Hex1b diagnostics socket was not cleaned up at {socketPath}");
    }

    [Fact(Timeout = 30_000)]
    public async Task SessionsCapture_ReturnsScreenContent_ViaRealSocket()
    {
        var (terminal, app, filter, listener, state, runTask, cts) = await StartTuiAsync();
        try
        {
            // Send the same capture request that `dotsider sessions capture <pid>` uses
            var requestJson = JsonSerializer.Serialize(
                new { method = "capture", format = "text" }, DotsiderJsonOptions.Default);

            var responseJson = await DotsiderClient.SendRawAsync(filter.SocketPath, requestJson, TestContext.Current.CancellationToken);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            Assert.True(response.GetProperty("success").GetBoolean(), "Capture request failed");
            Assert.True(response.TryGetProperty("data", out var data), "Capture response missing data");

            var content = data.GetString()!;
            Assert.NotEmpty(content);
            Assert.Contains("dotsider", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await StopAndDisposeAsync(terminal, app, filter, listener, state, runTask, cts);
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task CaptureScreen_ReturnsAssemblyContent_ViaRealSocket()
    {
        var (terminal, app, filter, listener, state, runTask, cts) = await StartTuiAsync();
        try
        {
            // Exercise the same code path that MCP capture_screen uses
            var requestJson = JsonSerializer.Serialize(
                new { method = "capture", format = "text" }, DotsiderJsonOptions.Default);

            var responseJson = await DotsiderClient.SendRawAsync(filter.SocketPath, requestJson, TestContext.Current.CancellationToken);
            var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

            Assert.True(response.GetProperty("success").GetBoolean());
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
