using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// A test Unix domain socket server that responds to raw JSON requests.
/// </summary>
internal sealed class TestRawJsonSocket : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _lifetimeGate = new();
    private readonly Socket _listener;
    private readonly string _socketPath;
    private Task? _acceptTask;
    private Task? _disposeTask;
    private Func<JsonElement, string>? _handler;
    private int _shutdownStarted;

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
        {
            File.Delete(socketPath);
        }

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        _listener.Listen(8);
    }

    /// <summary>
    /// Registers a handler that receives raw JSON and returns a raw JSON response string.
    /// </summary>
    public void OnRequest(Func<JsonElement, string> handler)
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_shutdownStarted != 0, this);

            if (_acceptTask is not null)
            {
                throw new InvalidOperationException("The handler must be registered before the socket server starts.");
            }

            _handler = handler;
        }
    }

    /// <summary>
    /// Starts accepting connections in the background.
    /// </summary>
    public void Start()
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_shutdownStarted != 0, this);

            if (_acceptTask is not null)
            {
                throw new InvalidOperationException("The socket server has already started.");
            }

            _acceptTask = AcceptLoop(_cts.Token);
        }
    }

    private async Task AcceptLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket client;
            ExceptionDispatchInfo? handlerFailure = null;
            var stopAfterConnection = false;
            try
            {
                client = await _listener.AcceptAsync(cancellationToken);
            }
            catch (Exception ex) when (IsExpectedShutdownException(ex, cancellationToken))
            {
                break;
            }

            try
            {
                await using var stream = new NetworkStream(client, ownsSocket: true);
                using var reader = new StreamReader(stream, leaveOpen: true);
                await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var request = JsonSerializer.Deserialize<JsonElement>(line);
                string response;
                if (_handler is { } handler)
                {
                    try
                    {
                        response = handler(request);
                    }
                    catch (Exception ex)
                    {
                        handlerFailure = ExceptionDispatchInfo.Capture(ex);
                        response = string.Empty;
                    }
                }
                else
                {
                    response = JsonSerializer.Serialize(new { success = false, error = "No handler" });
                }

                if (handlerFailure is null)
                {
                    await writer.WriteLineAsync(response.AsMemory(), cancellationToken);
                }
            }
            catch (Exception ex) when (IsExpectedShutdownException(ex, cancellationToken))
            {
                stopAfterConnection = true;
            }

            handlerFailure?.Throw();
            if (stopAfterConnection)
            {
                break;
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        Task disposeTask;
        lock (_lifetimeGate)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _shutdownStarted, 1);
                _disposeTask = DisposeCoreAsync();
            }

            disposeTask = _disposeTask;
        }

        await disposeTask;
    }

    private async Task DisposeCoreAsync()
    {
        _cts.Cancel();
        _listener.Dispose();

        try
        {
            if (_acceptTask is not null)
            {
                await _acceptTask;
            }
        }
        finally
        {
            _cts.Dispose();

            if (File.Exists(_socketPath))
            {
                File.Delete(_socketPath);
            }
        }
    }

    private bool IsExpectedShutdownException(Exception exception, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _shutdownStarted) == 0 || !cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception switch
        {
            OperationCanceledException canceled => canceled.CancellationToken == cancellationToken,
            IOException or ObjectDisposedException or SocketException => true,
            _ => false,
        };
    }
}
