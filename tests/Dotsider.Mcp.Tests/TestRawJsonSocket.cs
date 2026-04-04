using System.Net.Sockets;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

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
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException)
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
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Swallow errors in test server
            }
        }
    }

    /// <inheritdoc/>
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
