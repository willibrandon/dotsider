using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Widgets;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end diagnostics-socket tests for Native AOT analysis commands used by MCP session tools.
/// </summary>
[TestClass]
public class NativeAotSessionSocketTests : IAsyncDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;
    private DotsiderState? _state;
    private DotsiderDiagnosticsListener? _listener;
    private CancellationTokenSource? _appCts;
    private Task? _appTask;

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
        _listener.StartListening(overridePid: TestSocketIds.NextPid());

        _appCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _appTask = _app.RunAsync(_appCts.Token);
        await Task.Delay(100, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state is not null,
            TimeSpan.FromSeconds(10));

        return _listener.SocketPath!;
    }

    /// <summary>get-native-aot-info returns identity facts from a running Native AOT session.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeAotInfo_ReturnsSummary()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(Samples.NativeAotConsoleExe!, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-native-aot-info" }, ct);

        Assert.IsTrue(response.Success, response.Error);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.AreEqual("nativeAot", data.GetProperty("binaryKind").GetString());
        Assert.IsGreaterThan(0, data.GetProperty("readyToRunSections").GetInt32());
    }

    /// <summary>get-current-view reports the native tab 3 label for Native AOT sessions.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetCurrentView_NativeAot_Tab3LabelIsDisassembly()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(Samples.NativeAotConsoleExe!, ct);

        var navigate = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "navigate", TabId = TabId.IlInspector + 1 }, ct);
        Assert.IsTrue(navigate.Success, navigate.Error);
        await Task.Delay(500, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "get-current-view" }, ct);

        Assert.IsTrue(response.Success, response.Error);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.AreEqual(3, data.GetProperty("tab").GetInt32());
        Assert.AreEqual("Disassembly", data.GetProperty("tabLabel").GetString());
    }

    /// <summary>get-native-aot-size-contributors returns mstat contributors from a running session.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeAotSizeContributors_ReturnsRows()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(Samples.NativeAotConsoleExe!, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest
            {
                Method = "get-native-aot-size-contributors",
                Section = "Method",
                TopN = 5
            }, ct);

        Assert.IsTrue(response.Success, response.Error);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.IsGreaterThan(0, data.GetProperty("contributors").GetArrayLength());
    }

    /// <summary>list-methods uses the recovered Native AOT inventory through the session socket.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListMethods_ReturnsRecoveredNativeAotMethods()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(Samples.NativeAotConsoleExe!, ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "list-methods", TypeName = "Program" }, ct);

        Assert.IsTrue(response.Success, response.Error);
        var data = (response.Data as JsonElement?)!.Value;
        Assert.IsGreaterThan(0, data.GetArrayLength());
        TestAssert.All(data.EnumerateArray(), row =>
            Assert.AreEqual("RecoveredNativeAot", row.GetProperty("source").GetString()));
    }

    /// <summary>Disposes the diagnostics listener, state, and terminal.</summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _appCts?.Cancel();
        if (_listener is not null)
            await _listener.DisposeAsync();
        if (_appTask is not null)
        {
            try { await _appTask; }
            catch (OperationCanceledException) { }
        }
        _state?.Dispose();
        _app?.Dispose();
        if (_terminal is not null)
            await _terminal.DisposeAsync();
        _appCts?.Dispose();
    }
}
