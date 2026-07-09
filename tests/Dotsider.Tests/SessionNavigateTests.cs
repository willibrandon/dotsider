using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Widgets;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end tests for session navigation via the real diagnostics socket.
/// Starts a headless dotsider TUI, sends protocol requests over UDS, and
/// verifies the TUI state changes.
/// </summary>
[TestClass]
public class SessionNavigateTests : IAsyncDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

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
                _state ??= new DotsiderState(_app!, dllPath, pendingMutations);

                var dotsiderApp = new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
        });

        _listener = new DotsiderDiagnosticsListener(() => _state);
        _listener.StartListening(overridePid: TestSocketIds.NextPid());

        // Start the TUI and wait for first render
        _ = _app.RunAsync(ct);
        await Task.Delay(100, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state is not null,
            TimeSpan.FromSeconds(10));

        return (_app, _listener.SocketPath!);
    }

    /// <summary>
    /// Verifies navigate via socket changes active tab.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Navigate_ViaSocket_ChangesActiveTab()
    {
        var ct = CancellationToken.None;

        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.HelloWorldDll, ct);
        // Verify we start on tab 0 (General)
        var viewBefore = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.IsTrue(viewBefore.Success);
        var tabBefore = (viewBefore.Data as JsonElement?)?.GetProperty("tab").GetInt32();
        Assert.AreEqual(TabId.General + 1, tabBefore);

        // Navigate to Strings tab (user-facing 4) via the socket
        var navResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = TabId.Strings + 1 }, ct);
        Assert.IsTrue(navResponse.Success);

        // Give the TUI time to process the mutation
        await Task.Delay(500, ct);

        // Verify the tab actually changed
        var viewAfter = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.IsTrue(viewAfter.Success);
        var tabAfter = (viewAfter.Data as JsonElement?)?.GetProperty("tab").GetInt32();
        Assert.AreEqual(TabId.Strings + 1, tabAfter);
    }

    /// <summary>
    /// Reproduces the off-by-one: the CLI and TUI keyboard both show tabs as 1-8,
    /// so "navigate 1" should land on General (tab index 0), not PE/Metadata.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Navigate_Tab1_LandsOnGeneral_NotPeMetadata()
    {
        var ct = CancellationToken.None;

        var (_, socketPath) = await StartTuiWithDiagnosticsAsync(Samples.HelloWorldDll, ct);

        // First move off General so we can verify navigating back
        var navAway = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = TabId.IlInspector + 1 }, ct);
        Assert.IsTrue(navAway.Success);
        await Task.Delay(500, ct);

        // Navigate to tab 1 — the user-facing number for General
        var navResponse = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = 1 }, ct);
        Assert.IsTrue(navResponse.Success);
        await Task.Delay(500, ct);

        var view = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);
        Assert.IsTrue(view.Success);
        var activeTab = (view.Data as JsonElement?)?.GetProperty("tab").GetInt32();
        Assert.AreEqual(TabId.General + 1, activeTab);
    }

    /// <summary>
    /// Releases fixture state after tests complete.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (_listener is not null)
            await _listener.DisposeAsync();
        _state?.Dispose();
        if (_terminal is not null)
            await _terminal.DisposeAsync();
    }
}
