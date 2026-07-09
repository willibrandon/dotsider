using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace Dotsider.Website.Tests;

/// <summary>
/// Regression tests that launch the published single-file Website executable
/// as a subprocess and verify assembly resolution over WebSocket.
/// Reproduces the deployed scenario where RuntimeEnvironment.GetRuntimeDirectory()
/// returns the app directory with no loose BCL files.
/// </summary>
[TestClass]
public sealed class BundleResolutionRegressionTests : IAsyncDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Process? _serverProcess;
    private ClientWebSocket? _ws;

    /// <summary>
    /// Starts the published single-file Website on a free port and waits for health.
    /// </summary>
    private async Task<int> StartWebsiteAsync(CancellationToken ct)
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Samples.WebsitePublishedExe,
                WorkingDirectory = Samples.WebsitePublishedDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment =
                {
                    ["ASPNETCORE_URLS"] = $"http://localhost:{port}",
                    ["DOTNET_ENVIRONMENT"] = "Production",
                    // Point at the published sample payload (RichLibrary.dll sits alongside
                    // its .deps.json and Newtonsoft.Json.dll), exactly as systemd does in prod.
                    ["Demo__SampleAssembly"] = Samples.RichLibraryDll,
                    ["Demo__MaxSessions"] = "5",
                    ["Demo__SessionTimeoutMinutes"] = "1",
                    ["Demo__AllowedOrigins__0"] = "*"
                }
            }
        };

        _serverProcess.Start();

        using var httpClient = new HttpClient();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var response = await httpClient.GetAsync($"http://localhost:{port}/health", ct);
                if (response.IsSuccessStatusCode)
                    return port;
            }
            catch (HttpRequestException) { }
            await Task.Delay(100, ct);
        }

        throw new TimeoutException("Website server did not start in time");
    }

    /// <summary>
    /// Launches the published single-file Website, connects via WebSocket,
    /// and drills down into a System assembly reference.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DrillDown_Succeeds_InSingleFileHost()
    {
        var ct = CancellationToken.None;
        var port = await StartWebsiteAsync(ct);

        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri($"ws://localhost:{port}/ws"), ct);

        var output = new StringBuilder();
        var drainCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = DrainOutputAsync(_ws, output, drainCts.Token);

        await WaitForOutputAsync(output, "Assembly Name", TimeSpan.FromSeconds(5), ct);

        await SendKeysAsync("\x1b[B", ct);
        await Task.Delay(100, ct);
        await SendKeysAsync("\r", ct);

        await WaitForOutputAsync(output, "depth 2", TimeSpan.FromSeconds(5), ct);
        drainCts.Cancel();
    }

    /// <summary>
    /// Launches the published single-file Website, connects via WebSocket,
    /// navigates to <c>IlNavigationFixture.CallExternal</c> via confirmed IL search,
    /// and uses go-to-definition on the <c>Console.WriteLine</c> call to verify
    /// <see cref="Dotsider.Core.Analysis.ImplementationAssemblyResolver"/> works
    /// inside the single-file host.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GoToDef_CallExternal_NavigatesToSystemConsole_InSingleFileHost()
    {
        var ct = CancellationToken.None;
        var port = await StartWebsiteAsync(ct);

        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri($"ws://localhost:{port}/ws"), ct);

        var output = new StringBuilder();
        var drainCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = DrainOutputAsync(_ws, output, drainCts.Token);

        await WaitForOutputAsync(output, "Assembly Name", TimeSpan.FromSeconds(5), ct);

        // IL Inspector tab (key '3': General=1, PE/Metadata=2, IL Inspector=3)
        await SendKeysAsync("3", ct);
        await WaitForOutputAsync(output, "Select a method", TimeSpan.FromSeconds(5), ct);

        // '/' opens search, type "WriteLine", Enter confirms text-level IL search,
        // 'n' navigates to the first match — selects CallExternal and positions
        // the cursor on the call instruction containing "WriteLine".
        await SendKeysAsync("/", ct);
        await Task.Delay(200, ct);
        await SendKeysAsync("WriteLine", ct);
        await Task.Delay(200, ct);
        // Enter confirms search — computes text-level IL matches across all methods
        await SendKeysAsync("\r", ct);
        await Task.Delay(500, ct);
        // 'n' navigates to first match — NavigateToMatch selects the method and
        // sets IlPendingCursorMatch for the next render frame
        await SendKeysAsync("n", ct);
        await WaitForOutputAsync(output, "IL_", TimeSpan.FromSeconds(5), ct);
        // Wait for the render frame to process IlPendingCursorMatch
        await Task.Delay(500, ct);

        // Escape closes search, 'l' focuses the IL editor where the cursor
        // was positioned on the call instruction by NavigateToMatch
        await SendKeysAsync("\x1b", ct);
        await Task.Delay(200, ct);
        await SendKeysAsync("l", ct);
        await Task.Delay(200, ct);

        // Clear accumulated output so the assertion only matches post-navigation content
        lock (output) { output.Clear(); }

        // Go-to-definition on the call instruction
        await SendKeysAsync("\r", ct);

        // Title bar renders "System.Console.dll (depth 2)" — this exact format
        // from DotsiderApp.cs cannot appear in pre-navigation IL text.
        await WaitForOutputAsync(output, "System.Console.dll (depth 2)", TimeSpan.FromSeconds(5), ct);
        drainCts.Cancel();
    }

    /// <summary>
    /// Launches the published single-file Website, navigates to the Dep Graph tab, and
    /// asserts that the sample's <c>Newtonsoft.Json</c> reference resolves — the node
    /// renders without the <c>?</c> unresolved prefix. Catches the regression where
    /// the deploy pipeline shipped only <c>RichLibrary.dll</c> and left the NuGet
    /// dependencies behind. This test only passes when the sample is deployed as its
    /// full published payload (<c>RichLibrary.dll</c> + <c>RichLibrary.deps.json</c>
    /// + <c>Newtonsoft.Json.dll</c>, etc.) alongside the website.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DepGraph_NewtonsoftResolvesFromPublishedSample()
    {
        var ct = CancellationToken.None;
        var port = await StartWebsiteAsync(ct);

        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri($"ws://localhost:{port}/ws"), ct);

        var output = new StringBuilder();
        var drainCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = DrainOutputAsync(_ws, output, drainCts.Token);

        await WaitForOutputAsync(output, "Assembly Name", TimeSpan.FromSeconds(10), ct);

        // Tab 6 is the Dep Graph. Its transitive graph is built on a background thread
        // that opens and resolves each reference, which can take many seconds under CI
        // load, so wait for the resolved node itself — the exact text this test asserts
        // on — rather than an intermediate signal followed by a fixed delay.
        await SendKeysAsync("6", ct);
        await WaitForOutputAsync(output, "Newtonsoft.Json", TimeSpan.FromSeconds(30), ct);

        string captured;
        lock (output) { captured = output.ToString(); }

        Assert.DoesNotContain("? Newtonsoft", captured);
        Assert.DoesNotContain("! Newtonsoft", captured);

        drainCts.Cancel();
    }

    /// <summary>
    /// Sends a string as UTF-8 bytes over the WebSocket.
    /// </summary>
    private async Task SendKeysAsync(string keys, CancellationToken ct)
    {
        await _ws!.SendAsync(
            Encoding.UTF8.GetBytes(keys), WebSocketMessageType.Text, true, ct);
    }

    /// <summary>
    /// Drains WebSocket output into a <see cref="StringBuilder"/>.
    /// </summary>
    private static async Task DrainOutputAsync(WebSocket ws, StringBuilder output, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) break;
                lock (output) { output.Append(Encoding.UTF8.GetString(buffer, 0, result.Count)); }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    /// <summary>
    /// Polls output until it contains the specified text or timeout expires.
    /// </summary>
    private static async Task WaitForOutputAsync(
        StringBuilder output, string text, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            lock (output) { if (output.ToString().Contains(text)) return; }
            await Task.Delay(50, ct);
        }

        string captured;
        lock (output) { captured = output.ToString(); }
        Assert.Fail($"Timed out waiting for \"{text}\". Last output ({captured.Length} chars): "
            + $"...{captured[^Math.Min(500, captured.Length)..]}");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {

        if (_serverProcess is not null)
        {
            try { _serverProcess.Kill(entireProcessTree: true); }
            catch { /* already exited */ }
            try { await _serverProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { /* don't hang */ }
            _serverProcess.Dispose();
        }

        _ws?.Dispose();
    }
}
