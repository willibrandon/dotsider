using System.Diagnostics;
using Dotsider.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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

            logger?.LogDebug("Invoking tool {ToolName}", toolName);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = await next(context, cancellationToken);
                stopwatch.Stop();

                if (result.IsError == true)
                {
                    logger?.LogWarning("Tool {ToolName} returned error after {ElapsedMs}ms",
                        toolName, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    logger?.LogDebug("Tool {ToolName} completed in {ElapsedMs}ms",
                        toolName, stopwatch.ElapsedMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger?.LogError(ex, "Tool {ToolName} threw unhandled exception after {ElapsedMs}ms",
                    toolName, stopwatch.ElapsedMilliseconds);
                throw;
            }
        });
    });

await builder.Build().RunAsync();
