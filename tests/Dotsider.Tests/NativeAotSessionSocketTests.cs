using System.Collections.Concurrent;
using System.Text.Json;
using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end diagnostics-socket tests for Native AOT analysis commands used by MCP session tools.
/// </summary>
[Collection("SampleAssemblies")]
public class NativeAotSessionSocketTests(SampleAssemblyFixture samples) : IAsyncDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;
    private DotsiderState? _state;
    private DotsiderDiagnosticsListener? _listener;
    private CancellationTokenSource? _appCts;

    private async Task<string> StartTuiWithDiagnosticsAsync(string path, CancellationToken ct)
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
                _state ??= new DotsiderState(_app!, path, pendingMutations);
                var dotsiderApp = new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });

        _listener = new DotsiderDiagnosticsListener(() => _state);
        _listener.StartListening();

        _appCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = _app.RunAsync(_appCts.Token);
        await Task.Delay(100, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state is not null,
            TimeSpan.FromSeconds(10));

        return _listener.SocketPath!;
    }

    /// <summary>get-native-aot-info returns identity facts from a running Native AOT session.</summary>
    [Fact(Timeout = 30_000)]
    public async Task GetNativeAotInfo_ReturnsSummary()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(samples.NativeAotConsoleExe!, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-native-aot-info" }, ct);

        Assert.True(response.Success, response.Error);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.Equal("nativeAot", data.GetProperty("binaryKind").GetString());
        Assert.True(data.GetProperty("readyToRunSections").GetInt32() > 0);
    }

    /// <summary>get-current-view reports the native tab 3 label for Native AOT sessions.</summary>
    [Fact(Timeout = 30_000)]
    public async Task GetCurrentView_NativeAot_Tab3LabelIsDisassembly()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(samples.NativeAotConsoleExe!, ct);

        var navigate = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = TabId.IlInspector + 1 }, ct);
        Assert.True(navigate.Success, navigate.Error);
        await Task.Delay(500, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);

        Assert.True(response.Success, response.Error);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.Equal(3, data.GetProperty("tab").GetInt32());
        Assert.Equal("Disassembly", data.GetProperty("tabLabel").GetString());
    }

    /// <summary>get-native-aot-size-contributors returns mstat contributors from a running session.</summary>
    [Fact(Timeout = 30_000)]
    public async Task GetNativeAotSizeContributors_ReturnsRows()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(samples.NativeAotConsoleExe!, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest
            {
                Method = "get-native-aot-size-contributors",
                Section = "Method",
                TopN = 5
            }, ct);

        Assert.True(response.Success, response.Error);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.True(data.GetProperty("contributors").GetArrayLength() > 0);
    }

    /// <summary>list-methods uses the recovered Native AOT inventory through the session socket.</summary>
    [Fact(Timeout = 30_000)]
    public async Task ListMethods_ReturnsRecoveredNativeAotMethods()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var ct = TestContext.Current.CancellationToken;
        var socketPath = await StartTuiWithDiagnosticsAsync(samples.NativeAotConsoleExe!, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-methods", TypeName = "Program" }, ct);

        Assert.True(response.Success, response.Error);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.True(data.GetArrayLength() > 0);
        Assert.All(data.EnumerateArray(), row =>
            Assert.Equal("RecoveredNativeAot", row.GetProperty("source").GetString()));
    }

    /// <summary>Disposes the diagnostics listener, state, and terminal.</summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _appCts?.Cancel();
        if (_listener is not null)
            await _listener.DisposeAsync();
        _state?.Dispose();
        if (_terminal is not null)
            await _terminal.DisposeAsync();
        _appCts?.Dispose();
    }
}
