using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Tests that the connection limit (4 concurrent) works correctly.
/// Uses the full headless TUI stack with real assemblies.
/// </summary>
[TestClass]
public sealed class ConnectionLimitTests : IAsyncDisposable
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
        if (_listener is not null)
        {
            _listener.TestDelayHook = null;
            await _listener.DisposeAsync();
        }

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
    /// Verifies four connections all succeed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task FourConnections_AllSucceed()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        // Fire 4 concurrent requests — all should succeed
        var tasks = Enumerable.Range(0, 4)
            .Select(_ => DotsiderClient.SendAsync(socketPath,
                new DotsiderRequest { Method = "assembly-info" }, ct))
            .ToList();

        var responses = await Task.WhenAll(tasks);
        TestAssert.All(responses, r => Assert.IsTrue(r.Success));
    }

    /// <summary>
    /// Verifies an oversized fifth connection is bounded and rejected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task FifthOversizedConnection_IsRejected()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        // Use the test delay hook to hold 4 slots open
        var holdOpen = new TaskCompletionSource();
        _listener!.TestDelayHook = () => holdOpen.Task;

        // Open 4 connections that will hold slots
        var heldSockets = new List<(Socket socket, StreamWriter writer)>();
        for (var i = 0; i < 4; i++)
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
            var stream = new NetworkStream(socket, ownsSocket: false);
            var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
            var json = JsonSerializer.Serialize(
                new DotsiderRequest { Method = "assembly-info" }, DotsiderJsonContext.Protocol.Options);
            await writer.WriteLineAsync(json.AsMemory(), ct);
            heldSockets.Add((socket, writer));
        }

        // Wait for the 4 handlers to acquire their slots and enter the delay hook
        await Task.Delay(200, ct);

        // The fifth request exceeds the protocol limit. The saturated path must
        // discard it with bounded storage before returning the connection-limit error.
        var responseJson = await DotsiderClient.SendRawAsync(
            socketPath,
            new string('a', DotsiderProtocol.MaxRequestBytes + 1),
            ct);
        var response = JsonSerializer.Deserialize<DotsiderResponse>(
            responseJson,
            DotsiderJsonContext.Protocol.Options);

        Assert.IsNotNull(response);
        Assert.IsFalse(response.Success);
        Assert.Contains("too many", response.Error!, StringComparison.OrdinalIgnoreCase);

        // Release the held connections
        holdOpen.SetResult();
        foreach (var (socket, writer) in heldSockets)
        {
            await writer.DisposeAsync();
            socket.Dispose();
        }
    }

    /// <summary>
    /// Verifies slot freed allows new connection.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SlotFreed_AllowsNewConnection()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        // Hold 4 slots with a controllable hook
        var holdOpen = new TaskCompletionSource();
        _listener!.TestDelayHook = () => holdOpen.Task;

        var heldSockets = new List<(Socket socket, StreamWriter writer)>();
        for (var i = 0; i < 4; i++)
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
            var stream = new NetworkStream(socket, ownsSocket: false);
            var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
            var json = JsonSerializer.Serialize(
                new DotsiderRequest { Method = "assembly-info" }, DotsiderJsonContext.Protocol.Options);
            await writer.WriteLineAsync(json.AsMemory(), ct);
            heldSockets.Add((socket, writer));
        }

        await Task.Delay(200, ct);

        // Release all held connections
        holdOpen.SetResult();
        foreach (var (socket, writer) in heldSockets)
        {
            await writer.DisposeAsync();
            socket.Dispose();
        }

        // Wait for slots to be released
        await Task.Delay(200, ct);

        // Remove the delay hook so new connections proceed normally
        _listener.TestDelayHook = null;

        // New connection should succeed
        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);

        Assert.IsTrue(response.Success);
    }

    /// <summary>
    /// Verifies stalled client times out.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task StalledClient_TimesOut()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        // Connect but never send a newline — the read timeout (5s) should free the slot
        using var stalledSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await stalledSocket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

        // Wait for the read timeout to kick in (5s + margin)
        await Task.Delay(6_000, ct);

        // After timeout, a new connection should succeed
        var response = await DotsiderClient.SendAsync(socketPath,
            new DotsiderRequest { Method = "assembly-info" }, ct);

        Assert.IsTrue(response.Success);
    }

    /// <summary>
    /// Verifies shutdown with active connections completes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ShutdownWithActiveConnections_Completes()
    {
        var ct = CancellationToken.None;
        var socketPath = await StartTuiWithDiagnosticsAsync(ct);

        // Hold a connection slot open
        var holdOpen = new TaskCompletionSource();
        _listener!.TestDelayHook = () => holdOpen.Task;

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        var json = JsonSerializer.Serialize(
            new DotsiderRequest { Method = "assembly-info" }, DotsiderJsonContext.Protocol.Options);
        await writer.WriteLineAsync(json.AsMemory(), ct);

        await Task.Delay(200, ct);

        // Dispose should complete within a reasonable time (CTS cancellation
        // triggers the read timeout, which releases the slot, allowing drain)
        _listener.TestDelayHook = null;
        holdOpen.SetCanceled(ct);

        var disposeTask = _listener.DisposeAsync().AsTask();
        var completed = await Task.WhenAny(disposeTask, Task.Delay(10_000, ct));
        Assert.AreSame(disposeTask, completed);

        // Prevent double-dispose in DisposeAsync
        _listener = null;
    }
}
