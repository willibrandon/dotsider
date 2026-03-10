using System.Collections.Concurrent;
using System.Text.Json;
using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end tests for session navigation via the real diagnostics socket.
/// Starts a headless dotsider TUI, sends protocol requests over UDS, and
/// verifies the TUI state changes.
/// </summary>
[Collection("SampleAssemblies")]
public class SessionNavigateTests(SampleAssemblyFixture samples) : IAsyncDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;
    private DotsiderState? _state;
    private DotsiderDiagnosticsListener? _listener;

    /// <summary>
    /// Starts a headless dotsider TUI with the diagnostics socket listener,
    /// reproducing the full production stack.
    /// </summary>
    private async Task<(Hex1bApp app, string socketPath)> StartTuiWithDiagnosticsAsync(
        string dllPath, CancellationToken ct)
    {
        var pendingMutations = new ConcurrentQueue<Action<DotsiderState>>();

        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();

        _app = new Hex1bApp(
            ctx =>
            {
                if (_state is null)
                {
                    _state = new DotsiderState(_app!, dllPath, pendingMutations);
                }

                var dotsiderApp = new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });

        _listener = new DotsiderDiagnosticsListener(() => _state, pendingMutations);
        _listener.StartListening();

        // Start the TUI and wait for first render
        _ = _app.RunAsync(ct);

        await TestHelpers.WaitUntilAsync(
            () => _state is not null,
            TimeSpan.FromSeconds(5));

        return (_app, _listener.SocketPath!);
    }

    [Fact(Timeout = 15_000)]
    public async Task Navigate_ViaSocket_ChangesActiveTab()
    {
        var ct = TestContext.Current.CancellationToken;

        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(samples.HelloWorldDll, ct);
        // Verify we start on tab 0 (General)
        var viewBefore = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.True(viewBefore.Success);
        var tabBefore = (viewBefore.Data as JsonElement?)?.GetProperty("tab").GetInt32();
        Assert.Equal(TabId.General, tabBefore);

        // Navigate to Strings tab (3) via the socket
        var navResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = TabId.Strings }, ct);
        Assert.True(navResponse.Success);

        // Give the TUI time to process the mutation
        await Task.Delay(500, ct);

        // Verify the tab actually changed
        var viewAfter = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.True(viewAfter.Success);
        var tabAfter = (viewAfter.Data as JsonElement?)?.GetProperty("tab").GetInt32();
        Assert.Equal(TabId.Strings, tabAfter);
    }

    public async ValueTask DisposeAsync()
    {
        if (_listener is not null)
            await _listener.DisposeAsync();
        _state?.Dispose();
        if (_terminal is not null)
            await _terminal.DisposeAsync();
    }
}
