using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Website.Tests;

/// <summary>
/// Reproduces Issue #62: search input via WebSocket drops characters after '/'.
/// Uses a real WebSocket pair to exercise the same code path as the browser
/// (WebSocketPresentationAdapter.ReadInputAsync), not the headless path.
/// </summary>
[Collection("SampleAssemblies")]
public class SearchInputTests(SampleAssemblyFixture samples) : IAsyncDisposable
{
    private WebSocket? _serverWs;
    private WebSocket? _clientWs;
    private Socket? _serverSocket;
    private Socket? _clientSocket;
    private WebSocketPresentationAdapter? _presentation;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    /// <summary>
    /// Creates a connected WebSocket pair using TCP loopback sockets.
    /// Returns (clientWs, serverWs) where clientWs simulates the browser
    /// and serverWs is passed to WebSocketPresentationAdapter.
    /// </summary>
    private async Task<(WebSocket client, WebSocket server)> CreateWebSocketPairAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var connectTask = _clientSocket.ConnectAsync(IPAddress.Loopback, port);
        _serverSocket = await listener.AcceptSocketAsync();
        await connectTask;
        listener.Stop();

        var clientStream = new NetworkStream(_clientSocket, ownsSocket: false);
        var serverStream = new NetworkStream(_serverSocket, ownsSocket: false);

        _serverWs = WebSocket.CreateFromStream(serverStream, new WebSocketCreationOptions { IsServer = true });
        _clientWs = WebSocket.CreateFromStream(clientStream, new WebSocketCreationOptions { IsServer = false });

        return (_clientWs, _serverWs);
    }

    /// <summary>
    /// Validates that search input works via WebSocket. Pressing '/' activates the
    /// search bar and the subsequent characters reach the TextBox. This requires
    /// DotsiderApp to be reused across renders so _initialFocusRequested isn't reset.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SearchInput_ViaWebSocket_CharactersReachSearchBar()
    {
        var ct = TestContext.Current.CancellationToken;

        // 1. Create WebSocket pair
        var (clientWs, serverWs) = await CreateWebSocketPairAsync();

        // 2. Wire up DotsiderApp through WebSocketPresentationAdapter
        //    (mirrors src/Dotsider.Website/Program.cs:149-195)
        _presentation = new WebSocketPresentationAdapter(serverWs, 120, 36, enableMouse: true);
        var workload = new Hex1bAppWorkloadAdapter(_presentation.Capabilities);

        _terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            PresentationAdapter = _presentation,
            WorkloadAdapter = workload
        });

        DotsiderApp? dotsiderApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                _state ??= new DotsiderState(_hex1bApp!, samples.RichLibraryDll);
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                Theme = DotsiderTheme.Create(),
                EnableMouse = true,
                EnableInputCoalescing = false
            });

        var runTask = _hex1bApp.RunAsync(ct);

        // 3. Drain output in background to prevent server-side send buffer
        //    from blocking the render loop
        var output = new StringBuilder();
        var drainCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var drainTask = DrainOutputAsync(clientWs, output, drainCts.Token);

        // 4. Wait for app to render (General tab shows "Assembly Name")
        await WaitForOutputAsync(output, "Assembly Name", TimeSpan.FromSeconds(10), ct);

        // 5. Send '/' to trigger search via WebSocket
        //    (same as xterm.js: term.onData("/") → ws.send("/"))
        await clientWs.SendAsync(
            Encoding.UTF8.GetBytes("/"),
            WebSocketMessageType.Text,
            endOfMessage: true,
            ct);

        // Send "test" immediately after '/' with no delay — exercises the back-to-back
        // scenario where characters arrive before the render cycle applies focus.
        // With input coalescing disabled, each event gets its own render cycle.
        await clientWs.SendAsync(
            Encoding.UTF8.GetBytes("test"),
            WebSocketMessageType.Text,
            endOfMessage: true,
            ct);

        // 6. Wait for processing
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(3))
        {
            if (_state?.Search[0].Query == "test") break;
            await Task.Delay(100, ct);
        }

        // 7. Assert — characters should reach the TextBox
        Assert.NotNull(_state);
        Assert.True(_state.Search[0].IsActive, "Search should be active after pressing '/'");
        Assert.Equal("test", _state.Search[0].Query);

        // Cleanup
        drainCts.Cancel();
        try { await drainTask; } catch (OperationCanceledException) { }
    }

    private static async Task DrainOutputAsync(WebSocket ws, StringBuilder output, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) break;
                lock (output)
                {
                    output.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    private static async Task WaitForOutputAsync(
        StringBuilder output, string text, TimeSpan timeout, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            lock (output)
            {
                if (output.ToString().Contains(text)) return;
            }
            await Task.Delay(100, ct);
        }
        Assert.Fail($"Timed out after {timeout.TotalSeconds:F0}s waiting for output containing \"{text}\"");
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();

        if (_presentation != null)
            await _presentation.DisposeAsync();

        if (_clientWs != null)
        {
            try
            {
                if (_clientWs.State == WebSocketState.Open)
                    await _clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch { }
            _clientWs.Dispose();
        }

        _serverWs?.Dispose();
        _clientSocket?.Dispose();
        _serverSocket?.Dispose();
    }
}
