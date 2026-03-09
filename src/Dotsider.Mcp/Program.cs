using System.Diagnostics;
using System.Reflection;
using Dotsider.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

if (args is ["--help" or "-h"])
{
    Console.WriteLine("dotsider-mcp — MCP server for .NET assembly analysis");
    Console.WriteLine("This server communicates via stdin/stdout using the MCP protocol.");
    Console.WriteLine("Configure it in your AI assistant's MCP settings.");
    Console.WriteLine();
    Console.WriteLine("  -h, --help       Show this help");
    Console.WriteLine("  --version        Show version");
    return;
}

if (args is ["--version"])
{
    Console.WriteLine(typeof(DotsiderSessionManager).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "unknown");
    return;
}

var builder = Host.CreateApplicationBuilder(args);

// MCP uses stdout for protocol communication — send all logs to stderr
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<DotsiderSessionManager>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly()
    .WithRequestFilters(filters =>
    {
        filters.AddCallToolFilter(next => async (context, cancellationToken) =>
        {
            var logger = context.Services?.GetService<ILogger<DotsiderSessionManager>>();
            var toolName = context.Params?.Name ?? "unknown";

            if (logger is not null) Log.ToolInvoking(logger, toolName);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await next(context, cancellationToken);
                stopwatch.Stop();

                if (logger is not null)
                {
                    if (result.IsError == true)
                        Log.ToolReturnedError(logger, toolName, stopwatch.ElapsedMilliseconds);
                    else
                        Log.ToolCompleted(logger, toolName, stopwatch.ElapsedMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                if (logger is not null)
                    Log.ToolUnhandledException(logger, ex, toolName, stopwatch.ElapsedMilliseconds);
                throw;
            }
        });
    });

await builder.Build().RunAsync();
