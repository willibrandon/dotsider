using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Dotsider.Mcp.Tests;

using MCP = ModelContextProtocol.Server.McpServer;

/// <summary>
/// Base class for MCP server integration tests using in-memory pipe transport.
/// Sets up a full MCP server with the dotsider session manager and tools.
/// </summary>
public abstract class McpServerTestBase : IAsyncDisposable
{
    private readonly Pipe _clientToServerPipe = new();
    private readonly Pipe _serverToClientPipe = new();
    private readonly CancellationTokenSource _cts = new();
    private Task _serverTask = Task.CompletedTask;
    private MCP? _server;
    private ServiceProvider? _serviceProvider;
    private ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Wires up the DI container, in-memory pipe transport, and tool/prompt discovery.
    /// </summary>
    protected McpServerTestBase()
    {
        ServiceCollection = new ServiceCollection();

        ServiceCollection.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        ServiceCollection.AddSingleton<DotsiderSessionManager>();

        var mcpServerAssembly = typeof(DotsiderSessionManager).Assembly;
        McpServerBuilder = ServiceCollection
            .AddMcpServer()
            .WithStreamServerTransport(
                _clientToServerPipe.Reader.AsStream(),
                _serverToClientPipe.Writer.AsStream())
            .WithToolsFromAssembly(mcpServerAssembly)
            .WithPromptsFromAssembly(mcpServerAssembly);
    }

    /// <summary>
    /// DI service collection derived tests may augment before calling <see cref="StartServerAsync"/>.
    /// </summary>
    protected ServiceCollection ServiceCollection { get; }
    /// <summary>
    /// Builder used to register additional tools or prompts prior to server startup.
    /// </summary>
    protected IMcpServerBuilder McpServerBuilder { get; }

    /// <summary>
    /// The MCP server instance under test; built once per test by <see cref="StartServerAsync"/>.
    /// </summary>
    protected MCP Server => _server
        ?? throw new InvalidOperationException("Server not started. Call StartServerAsync first.");

    /// <summary>
    /// Root service provider created when the server starts; exposes resolved services to tests.
    /// </summary>
    protected ServiceProvider ServiceProvider => _serviceProvider
        ?? throw new InvalidOperationException("Server not started. Call StartServerAsync first.");

    /// <summary>
    /// Resolves the dotsider session manager from the active service provider.
    /// </summary>
    protected DotsiderSessionManager SessionManager =>
        ServiceProvider.GetRequiredService<DotsiderSessionManager>();

    /// <summary>
    /// Initializes the DI container, builds the server, and readies it for client connections.
    /// </summary>
    protected async Task StartServerAsync()
    {
        _serviceProvider = ServiceCollection.BuildServiceProvider(validateScopes: true);
        _loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
        _server = _serviceProvider.GetRequiredService<MCP>();
        _serverTask = _server.RunAsync(_cts.Token);

        await Task.Delay(50);
    }

    /// <summary>
    /// Creates an MCP client bound to the in-memory stream transport against the running server.
    /// </summary>
    protected async Task<McpClient> CreateClientAsync(McpClientOptions? clientOptions = null)
    {
        return await McpClient.CreateAsync(
            new StreamClientTransport(
                serverInput: _clientToServerPipe.Writer.AsStream(),
                serverOutput: _serverToClientPipe.Reader.AsStream(),
                _loggerFactory),
            clientOptions: clientOptions,
            loggerFactory: _loggerFactory,
            cancellationToken: _cts.Token);
    }

    /// <summary>
    /// Cancellation token that fires when the test completes or the base class is disposed.
    /// </summary>
    protected CancellationToken TestCancellationToken => _cts.Token;

    /// <summary>
    /// Extracts the first text block from a tool result for assertion.
    /// </summary>
    protected static string? GetTextContent(CallToolResult result)
    {
        var textBlock = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        return textBlock?.Text;
    }

    /// <summary>
    /// Tears down the server, completes the pipes, and disposes the service provider.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        _clientToServerPipe.Writer.Complete();
        _serverToClientPipe.Writer.Complete();

        try
        {
            await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
        }
        catch (TimeoutException)
        {
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        _cts.Dispose();

        GC.SuppressFinalize(this);
    }
}
