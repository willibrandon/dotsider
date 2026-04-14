using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Dotsider.Core.Protocol;
using Dotsider.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks measuring MCP tool call round-trip through the in-process pipe transport.
/// Captures the full pipeline: JSON-RPC framing, DI resolution, filter execution,
/// tool dispatch, analysis, JSON serialization, and response framing.
/// </summary>
/// <remarks>
/// DiscoverSessions creates real Unix domain sockets in an isolated temp directory,
/// exercising the full discovery path: file scan, socket connect, assembly-info probe,
/// stale socket cleanup, and JSON serialization.
/// </remarks>
[MemoryDiagnoser]
public class McpToolBenchmarks
{
    private const int SessionSocketCount = 5;

    private Pipe _clientToServer = null!;
    private Pipe _serverToClient = null!;
    private ServiceProvider _serviceProvider = null!;
    private McpServer _server = null!;
    private McpClient _client = null!;
    private Task _serverTask = null!;
    private CancellationTokenSource _cts = null!;
    private string _coreLibPath = null!;
    private string _socketDir = null!;

    // Real UDS listeners for session discovery benchmarks
    private readonly List<(Socket Listener, string Path, Task Loop, CancellationTokenSource Cts)> _sessionSockets = [];

    /// <summary>
    /// Stands up an in-process MCP client/server pair over pipes and listens on a cluster of UDS sockets.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _coreLibPath = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Private.CoreLib.dll");
        // Keep path short — macOS limits UDS paths to 104 characters
        _socketDir = Path.Combine(Path.GetTempPath(), $"ds-{Guid.NewGuid().ToString("N")[..8]}");

        SetupSessionSockets();
        SetupMcpServer();
    }

    private void SetupMcpServer()
    {
        _clientToServer = new Pipe();
        _serverToClient = new Pipe();
        _cts = new CancellationTokenSource();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddSingleton(new DotsiderSessionManager(_socketDir));

        var mcpAssembly = typeof(DotsiderSessionManager).Assembly;
        services.AddMcpServer()
            .WithStreamServerTransport(
                _clientToServer.Reader.AsStream(),
                _serverToClient.Writer.AsStream())
            .WithToolsFromAssembly(mcpAssembly)
            .WithPromptsFromAssembly(mcpAssembly);

        _serviceProvider = services.BuildServiceProvider(validateScopes: true);
        _server = _serviceProvider.GetRequiredService<McpServer>();
        _serverTask = _server.RunAsync(_cts.Token);

        _client = McpClient.CreateAsync(
            new StreamClientTransport(
                serverInput: _clientToServer.Writer.AsStream(),
                serverOutput: _serverToClient.Reader.AsStream()),
            loggerFactory: _serviceProvider.GetService<ILoggerFactory>(),
            cancellationToken: _cts.Token).GetAwaiter().GetResult();
    }

    private void SetupSessionSockets()
    {
        Directory.CreateDirectory(_socketDir);

        for (var i = 0; i < SessionSocketCount; i++)
        {
            var pid = 90000 + i;
            var socketPath = Path.Combine(_socketDir, $"{pid}.dotsider.socket");

            if (File.Exists(socketPath))
                File.Delete(socketPath);

            var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            listener.Listen(5);

            var cts = new CancellationTokenSource();
            var loop = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await listener.AcceptAsync(cts.Token);
                        _ = HandleSessionRequestAsync(client);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (ObjectDisposedException) { break; }
                }
            });

            _sessionSockets.Add((listener, socketPath, loop, cts));
        }
    }

    /// <summary>
    /// Tears down the MCP client, server task, socket listeners, and temp directory.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _cts.Cancel();
        _clientToServer.Writer.Complete();
        _serverToClient.Writer.Complete();
        try { _serverTask.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _serviceProvider.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _cts.Dispose();

        foreach (var (listener, path, loop, cts) in _sessionSockets)
        {
            cts.Cancel();
            listener.Close();
            listener.Dispose();
            try { loop.Wait(TimeSpan.FromSeconds(2)); } catch { }
            if (File.Exists(path)) File.Delete(path);
            cts.Dispose();
        }

        _sessionSockets.Clear();

        try { Directory.Delete(_socketDir, recursive: true); } catch { }
    }

    // --- Direct-mode tools ---

    /// <summary>
    /// Round-trips the get_assembly_info tool through the full MCP pipeline against CoreLib.
    /// </summary>
    [Benchmark(Description = "GetAssemblyInfo (CoreLib)")]
    public async Task<string> GetAssemblyInfo()
    {
        var result = await _client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = _coreLibPath });
        return result.Content.OfType<TextContentBlock>().First().Text!;
    }

    /// <summary>
    /// Round-trips the list_types tool to characterize bulk TypeDef serialization cost.
    /// </summary>
    [Benchmark(Description = "ListTypes (CoreLib)")]
    public async Task<string> ListTypes()
    {
        var result = await _client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?> { ["assemblyPath"] = _coreLibPath });
        return result.Content.OfType<TextContentBlock>().First().Text!;
    }

    /// <summary>
    /// Round-trips the get_size_breakdown tool, which walks the full type/method tree.
    /// </summary>
    [Benchmark(Description = "GetSizeBreakdown (CoreLib)")]
    public async Task<string> GetSizeBreakdown()
    {
        var result = await _client.CallToolAsync(
            "get_size_breakdown",
            new Dictionary<string, object?> { ["assemblyPath"] = _coreLibPath });
        return result.Content.OfType<TextContentBlock>().First().Text!;
    }

    /// <summary>
    /// Round-trips the disassemble_method tool for a single well-known method.
    /// </summary>
    [Benchmark(Description = "DisassembleMethod (single)")]
    public async Task<string> DisassembleMethod()
    {
        var result = await _client.CallToolAsync(
            "disassemble_method",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = _coreLibPath,
                ["typeName"] = "Object",
                ["methodName"] = "ToString",
            });
        return result.Content.OfType<TextContentBlock>().First().Text!;
    }

    /// <summary>
    /// Round-trips the extract_strings tool across CoreLib's full string heap.
    /// </summary>
    [Benchmark(Description = "ExtractStrings (CoreLib)")]
    public async Task<string> ExtractStrings()
    {
        var result = await _client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?> { ["assemblyPath"] = _coreLibPath });
        return result.Content.OfType<TextContentBlock>().First().Text!;
    }

    // --- Session discovery (full path: scan + connect + probe) ---

    /// <summary>
    /// Exercises the full session discovery path: socket scan, connect, probe, and stale cleanup across five listeners.
    /// </summary>
    [Benchmark(Description = "DiscoverSessions (5 sockets)")]
    public async Task<string> DiscoverSessions()
    {
        var result = await _client.CallToolAsync(
            "discover_dotsider_sessions",
            new Dictionary<string, object?>());
        return result.Content.OfType<TextContentBlock>().First().Text!;
    }

    // --- Socket handler ---

    private static async Task HandleSessionRequestAsync(Socket client)
    {
        try
        {
            await using var stream = new NetworkStream(client, ownsSocket: true);
            using var reader = new StreamReader(stream, leaveOpen: true);
            await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) return;

            var response = DotsiderResponse.Ok(new
            {
                AssemblyName = "BenchmarkAssembly",
                AssemblyVersion = "1.0.0.0",
                HasMetadata = true,
                TypeCount = 42,
                MethodCount = 100,
            });

            await writer.WriteLineAsync(
                JsonSerializer.Serialize(response, DotsiderJsonOptions.Default));
        }
        catch { }
    }
}
