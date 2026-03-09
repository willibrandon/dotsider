using System.Net.Sockets;
using System.Text.Json;
using Dotsider.Core.Protocol;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Creates a real Unix domain socket at the standard dotsider path that responds
/// to protocol requests. Used for integration testing session discovery and communication.
/// </summary>
internal sealed class TestDotsiderSocket : IAsyncDisposable
{
    private readonly Socket _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private readonly Dictionary<string, Func<DotsiderRequest, DotsiderResponse>> _handlers = new();

    /// <summary>The PID this socket pretends to be.</summary>
    public int Pid { get; }

    /// <summary>The socket file path.</summary>
    public string SocketPath { get; }

    public TestDotsiderSocket(int pid, string assemblyPath)
    {
        Pid = pid;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotsider", "sockets");
        Directory.CreateDirectory(dir);
        SocketPath = Path.Combine(dir, $"{pid}.dotsider.socket");

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

        _acceptLoop = Task.Run(() => AcceptConnectionsAsync(_cts.Token));
    }

    /// <summary>
    /// Registers a handler for a specific protocol method.
    /// </summary>
    public void OnMethod(string method, Func<DotsiderRequest, DotsiderResponse> handler)
    {
        _handlers[method.ToLowerInvariant()] = handler;
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptAsync(ct);
                _ = HandleConnectionAsync(client);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }

    private async Task HandleConnectionAsync(Socket client)
    {
        try
        {
            await using var stream = new NetworkStream(client, ownsSocket: true);
            using var reader = new StreamReader(stream, leaveOpen: true);
            await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) return;

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
                    response = handler(request);
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

            await writer.WriteLineAsync(
                JsonSerializer.Serialize(response, DotsiderJsonOptions.Default));
        }
        catch
        {
            // Connection-level errors are silently dropped (same as real listener)
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Close();
        _listener.Dispose();

        try { await _acceptLoop; } catch { }

        if (File.Exists(SocketPath))
            File.Delete(SocketPath);

        _cts.Dispose();
    }
}
