using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dotsider.Core.Protocol;

namespace Dotsider.Tests;

/// <summary>
/// A test Unix domain socket server that responds to dotsider protocol requests.
/// Register handlers per method name; unhandled methods return an error response.
/// </summary>
internal sealed class TestDotsiderSocket : IAsyncDisposable
{
    private readonly Socket _listener;
    private readonly string _socketPath;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, Func<DotsiderRequest, object?>> _handlers = new();
    private Task? _acceptTask;

    /// <summary>Gets the Unix domain socket path this server is listening on.</summary>
    public string SocketPath => _socketPath;

    /// <summary>
    /// Creates a new test socket server at the specified path.
    /// </summary>
    public TestDotsiderSocket(string socketPath)
    {
        _socketPath = socketPath;

        // Ensure the directory exists and clean up any stale socket
        var dir = Path.GetDirectoryName(socketPath)!;
        Directory.CreateDirectory(dir);
        if (File.Exists(socketPath))
            File.Delete(socketPath);

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        _listener.Listen(8);
    }

    /// <summary>
    /// Registers a handler for a protocol method. The handler returns the Data payload.
    /// </summary>
    public void On(string method, Func<DotsiderRequest, object?> handler)
    {
        _handlers[method] = handler;
    }

    /// <summary>
    /// Starts accepting connections in the background.
    /// </summary>
    public void Start()
    {
        _acceptTask = AcceptLoop(_cts.Token);
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await _listener.AcceptAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Handle each connection inline (test server, single-threaded is fine)
            try
            {
                await using var stream = new NetworkStream(client, ownsSocket: true);
                using var reader = new StreamReader(stream, leaveOpen: true);
                await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line))
                    continue;

                var request = JsonSerializer.Deserialize<DotsiderRequest>(line, DotsiderJsonOptions.Default);
                DotsiderResponse response;

                if (request is null || string.IsNullOrEmpty(request.Method))
                {
                    response = DotsiderResponse.Fail("Empty request");
                }
                else if (_handlers.TryGetValue(request.Method, out var handler))
                {
                    var data = handler(request);
                    response = DotsiderResponse.Ok(data);
                }
                else
                {
                    response = DotsiderResponse.Fail($"Unknown method: {request.Method}");
                }

                var responseJson = JsonSerializer.Serialize(response, DotsiderJsonOptions.Default);
                await writer.WriteLineAsync(responseJson.AsMemory(), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Swallow errors in test server
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Dispose();

        if (_acceptTask is not null)
        {
            try { await _acceptTask; }
            catch (OperationCanceledException) { }
        }

        _cts.Dispose();

        if (File.Exists(_socketPath))
            File.Delete(_socketPath);
    }
}

/// <summary>
/// A test Unix domain socket server that responds to raw JSON requests.
/// </summary>
internal sealed class TestRawJsonSocket : IAsyncDisposable
{
    private readonly Socket _listener;
    private readonly string _socketPath;
    private readonly CancellationTokenSource _cts = new();
    private Func<JsonElement, string>? _handler;
    private Task? _acceptTask;

    /// <summary>Gets the Unix domain socket path this server is listening on.</summary>
    public string SocketPath => _socketPath;

    /// <summary>
    /// Creates a new test raw JSON socket server at the specified path.
    /// </summary>
    public TestRawJsonSocket(string socketPath)
    {
        _socketPath = socketPath;

        var dir = Path.GetDirectoryName(socketPath)!;
        Directory.CreateDirectory(dir);
        if (File.Exists(socketPath))
            File.Delete(socketPath);

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        _listener.Listen(8);
    }

    /// <summary>
    /// Registers a handler that receives raw JSON and returns a raw JSON response string.
    /// </summary>
    public void OnRequest(Func<JsonElement, string> handler)
    {
        _handler = handler;
    }

    public void Start()
    {
        _acceptTask = AcceptLoop(_cts.Token);
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await _listener.AcceptAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await using var stream = new NetworkStream(client, ownsSocket: true);
                using var reader = new StreamReader(stream, leaveOpen: true);
                await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line))
                    continue;

                var request = JsonSerializer.Deserialize<JsonElement>(line);
                var response = _handler?.Invoke(request)
                    ?? JsonSerializer.Serialize(new { success = false, error = "No handler" });

                await writer.WriteLineAsync(response.AsMemory(), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Swallow errors in test server
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Dispose();

        if (_acceptTask is not null)
        {
            try { await _acceptTask; }
            catch (OperationCanceledException) { }
        }

        _cts.Dispose();

        if (File.Exists(_socketPath))
            File.Delete(_socketPath);
    }
}
