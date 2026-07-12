using Dotsider.Core.Protocol;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Creates a real Unix domain socket at the standard dotsider path that responds
/// to protocol requests. Used for integration testing session discovery and communication.
/// </summary>
internal sealed class TestDotsiderSocket : IAsyncDisposable
{
    private readonly List<Task> _connectionTasks = [];
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, Func<DotsiderRequest, DotsiderResponse>> _handlers = [];
    private readonly Lock _lifetimeGate = new();
    private readonly Socket _listener;
    private Task? _acceptTask;
    private Task? _disposeTask;
    private int _shutdownStarted;

    /// <summary>The PID this socket pretends to be.</summary>
    public int Pid { get; }

    /// <summary>The socket file path.</summary>
    public string SocketPath { get; }

    /// <summary>
    /// Creates a test socket for the specified process and assembly.
    /// </summary>
    public TestDotsiderSocket(int pid, string assemblyPath)
        : this(
            pid,
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dotsider", "sockets", $"{pid}.dotsider.socket"),
            assemblyPath)
    {
    }

    /// <summary>
    /// Creates a test socket at an explicit path for tests that do not exercise session discovery.
    /// </summary>
    public TestDotsiderSocket(string socketPath, string assemblyPath)
        : this(0, socketPath, assemblyPath)
    {
    }

    private TestDotsiderSocket(int pid, string socketPath, string assemblyPath)
    {
        Pid = pid;

        var dir = Path.GetDirectoryName(socketPath)!;
        Directory.CreateDirectory(dir);
        SocketPath = socketPath;

        if (File.Exists(SocketPath))
            File.Delete(SocketPath);

        // Register default handler for assembly-info (required for session discovery)
        _handlers["assembly-info"] = _ => DotsiderResponse.Ok(new
        {
            FilePath = assemblyPath,
            FileName = Path.GetFileName(assemblyPath),
            FileSize = File.Exists(assemblyPath) ? new FileInfo(assemblyPath).Length : 0L,
            AssemblyName = Path.GetFileNameWithoutExtension(assemblyPath),
            AssemblyVersion = "1.0.0.0",
            TargetFramework = ".NETCoreApp,Version=v10.0",
            Architecture = "AnyCPU",
            HasMetadata = true,
            TypeCount = 1,
            MethodCount = 1,
            AssemblyRefCount = 1
        });

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
        _listener.Listen(5);
    }

    /// <summary>
    /// Registers a handler for a specific protocol method.
    /// </summary>
    public void OnMethod(string method, Func<DotsiderRequest, DotsiderResponse> handler)
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_shutdownStarted != 0, this);

            if (_acceptTask is not null)
            {
                throw new InvalidOperationException("Handlers must be registered before the socket server starts.");
            }

            _handlers[method.ToLowerInvariant()] = handler;
        }
    }

    /// <summary>
    /// Starts accepting connections after all handlers have been registered.
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

            _acceptTask = AcceptConnectionsAsync(_cts.Token);
        }
    }

    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptAsync(cancellationToken);
                _connectionTasks.Add(HandleConnectionAsync(client, cancellationToken));
            }
            catch (Exception ex) when (IsExpectedShutdownException(ex, cancellationToken))
            {
                break;
            }
        }
    }

    private async Task HandleConnectionAsync(Socket client, CancellationToken cancellationToken)
    {
        ExceptionDispatchInfo? handlerFailure = null;
        try
        {
            await using var stream = new NetworkStream(client, ownsSocket: true);
            using var reader = new StreamReader(stream, leaveOpen: true);
            await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            DotsiderResponse response;
            try
            {
                var request = JsonSerializer.Deserialize<DotsiderRequest>(line, DotsiderJsonOptions.Default);
                if (request is null)
                {
                    response = DotsiderResponse.Fail("Empty request");
                }
                else if (_handlers.TryGetValue(request.Method.ToLowerInvariant(), out var handler))
                {
                    try
                    {
                        response = handler(request);
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
            }
            catch (JsonException ex)
            {
                response = DotsiderResponse.Fail($"Invalid JSON: {ex.Message}");
            }

            if (handlerFailure is null)
            {
                var responseJson = JsonSerializer.Serialize(response, DotsiderJsonOptions.Default);
                await writer.WriteLineAsync(responseJson.AsMemory(), cancellationToken);
            }
        }
        catch (Exception ex) when (IsExpectedShutdownException(ex, cancellationToken))
        {
        }

        handlerFailure?.Throw();
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

        ExceptionDispatchInfo? failure = null;
        try
        {
            if (_acceptTask is not null)
            {
                await _acceptTask;
            }
        }
        catch (Exception ex)
        {
            failure = ExceptionDispatchInfo.Capture(ex);
        }

        try
        {
            await Task.WhenAll(_connectionTasks);
        }
        catch (Exception ex)
        {
            failure ??= ExceptionDispatchInfo.Capture(ex);
        }

        _cts.Dispose();

        if (File.Exists(SocketPath))
        {
            File.Delete(SocketPath);
        }

        failure?.Throw();
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
