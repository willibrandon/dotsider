using Dotsider.Core.Protocol;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// A test Unix domain socket server that responds to dotsider protocol requests.
/// Register handlers per method name; unhandled methods return an error response.
/// </summary>
internal sealed class TestDotsiderSocket : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, Func<DotsiderRequest, object?>> _handlers = [];
    private readonly Lock _lifetimeGate = new();
    private readonly Socket _listener;
    private readonly string _socketPath;
    private Task? _acceptTask;
    private Task? _disposeTask;
    private int _shutdownStarted;

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
        {
            File.Delete(socketPath);
        }

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        _listener.Listen(8);
    }

    /// <summary>
    /// Registers or replaces a handler for a protocol method. The handler returns the Data payload.
    /// </summary>
    public void On(string method, Func<DotsiderRequest, object?> handler)
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_shutdownStarted != 0, this);

            _handlers[method] = handler;
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
            try
            {
                client = await _listener.AcceptAsync(cancellationToken);
            }
            catch (Exception ex) when (IsExpectedShutdownException(ex, cancellationToken))
            {
                break;
            }

            // Handle each connection inline (test server, single-threaded is fine)
            ExceptionDispatchInfo? handlerFailure = null;
            var stopAfterConnection = false;
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

                var request = JsonSerializer.Deserialize(
                    line, DotsiderJsonContext.Protocol.DotsiderRequest);
                DotsiderResponse response;

                if (request is null || string.IsNullOrEmpty(request.Method))
                {
                    response = DotsiderResponse.Fail("Empty request");
                }
                else if (_handlers.TryGetValue(request.Method, out var handler))
                {
                    try
                    {
                        var data = handler(request);
                        response = DotsiderResponse.Ok(TestJsonResponse.Element(data));
                    }
                    catch (Exception ex)
                    {
                        handlerFailure = ExceptionDispatchInfo.Capture(ex);
                        response = default!;
                    }
                }
                else
                {
                    response = DotsiderResponse.Fail($"Unknown method: {request.Method}");
                }

                if (handlerFailure is null)
                {
                    var responseJson = JsonSerializer.Serialize(
                        response, DotsiderJsonContext.Protocol.DotsiderResponse);
                    await writer.WriteLineAsync(responseJson.AsMemory(), cancellationToken);
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
