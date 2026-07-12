using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Tests that protocol versioning works correctly on both server and client sides.
/// Uses the full headless TUI stack with real assemblies.
/// </summary>
[TestClass]
public class ProtocolVersionTests : IAsyncDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;
    private DotsiderState? _state;
    private DotsiderDiagnosticsListener? _listener;
    private CancellationTokenSource? _appCts;
    private Task? _appTask;

    private async Task<string> StartTuiWithDiagnosticsAsync(CancellationToken ct)
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
                _state ??= new DotsiderState(_app!, Samples.HelloWorldDll, pendingMutations);
                var dotsiderApp = new DotsiderApp(_state);
                return Task.FromResult<Hex1b.Widgets.Hex1bWidget>(dotsiderApp.Build(ctx));
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

    /// <summary>
    /// Releases fixture state after tests complete.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        _appCts?.Cancel();
        if (_listener is not null) await _listener.DisposeAsync();
        if (_appTask is not null)
        {
            try { await _appTask; }
            catch (OperationCanceledException) { }
        }
        _state?.Dispose();
        _app?.Dispose();
        if (_terminal is not null) await _terminal.DisposeAsync();
        _appCts?.Dispose();
    }

    /// <summary>
    /// Verifies correct version succeeds.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task CorrectVersion_Succeeds()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);

        Assert.IsTrue(response.Success);
    }

    /// <summary>
    /// Verifies missing version is rejected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task MissingVersion_IsRejected()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        // Send raw JSON without "v" field — [JsonRequired] throws JsonException
        var rawResponse = await DotsiderClient.SendRawAsync(socketPath,
            """{"method":"assembly-info"}""", ct);

        var response = JsonSerializer.Deserialize<DotsiderResponse>(rawResponse, DotsiderJsonOptions.Default);
        Assert.IsNotNull(response);
        Assert.IsFalse(response.Success);
        Assert.Contains("JSON", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies wrong version is rejected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task WrongVersion_IsRejected()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        var rawResponse = await DotsiderClient.SendRawAsync(socketPath,
            """{"v":99,"method":"assembly-info"}""", ct);

        var response = JsonSerializer.Deserialize<DotsiderResponse>(rawResponse, DotsiderJsonOptions.Default);
        Assert.IsNotNull(response);
        Assert.IsFalse(response.Success);
        Assert.Contains("version mismatch", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies response contains version.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Response_ContainsVersion()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        var rawResponse = await DotsiderClient.SendRawAsync(socketPath,
            JsonSerializer.Serialize(new DotsiderRequest { Method = "assembly-info" },
                DotsiderJsonOptions.Default), ct);

        var doc = JsonDocument.Parse(rawResponse);
        Assert.IsTrue(doc.RootElement.TryGetProperty("v", out var v));
        Assert.AreEqual(1, v.GetInt32());
    }

    /// <summary>
    /// Verifies pre routing errors contain version.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task PreRoutingErrors_ContainVersion()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        // Version mismatch error carries "v":1
        var rawResponse = await DotsiderClient.SendRawAsync(socketPath,
            """{"v":99,"method":"assembly-info"}""", ct);
        var doc = JsonDocument.Parse(rawResponse);
        Assert.AreEqual(1, doc.RootElement.GetProperty("v").GetInt32());

        // Peer rejection error carries "v":1
        _listener!.ForceRejectPeers = true;
        rawResponse = await DotsiderClient.SendRawAsync(socketPath,
            JsonSerializer.Serialize(new DotsiderRequest { Method = "assembly-info" },
                DotsiderJsonOptions.Default), ct);
        doc = JsonDocument.Parse(rawResponse);
        Assert.AreEqual(1, doc.RootElement.GetProperty("v").GetInt32());
        _listener.ForceRejectPeers = false;
    }

    /// <summary>
    /// Verifies dotsider client rejects old server response.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task DotsiderClient_RejectsOldServerResponse()
    {
        var socketPath = GetUniqueSocketPath();
        await using var testSocket = new TestRawJsonSocket(socketPath);
        testSocket.OnRequest(_ => """{"success":true}""");
        testSocket.Start();

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" },
            CancellationToken.None);

        Assert.IsFalse(response.Success);
        Assert.Contains("server response", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies dotsider client rejects wrong server version.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task DotsiderClient_RejectsWrongServerVersion()
    {
        var socketPath = GetUniqueSocketPath();
        await using var testSocket = new TestRawJsonSocket(socketPath);
        testSocket.OnRequest(_ => """{"v":99,"success":true}""");
        testSocket.Start();

        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" },
            CancellationToken.None);

        Assert.IsFalse(response.Success);
        Assert.Contains("version mismatch", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetUniqueSocketPath()
    {
        return Path.Combine(Path.GetTempPath(), $"dp-{Guid.NewGuid():N}");
    }
}
