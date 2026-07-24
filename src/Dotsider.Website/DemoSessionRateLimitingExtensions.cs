using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace Dotsider.Website;

internal static class DemoSessionRateLimitingExtensions
{
    internal const string PolicyName = "demo-client-sessions";

    private const string NonWebSocketPartition = "non-websocket";
    private const string RejectionMessage = "Too many active sessions";
    private const string WebSocketPartition = "websocket";

    internal static IServiceCollection AddDemoSessionRateLimiting(
        this IServiceCollection services)
    {
        services.AddRateLimiter();
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<DemoOptions>>(static (options, configuredOptions) =>
            {
                var demoOptions = configuredOptions.Value;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    context => context.WebSockets.IsWebSocketRequest
                        ? RateLimitPartition.GetConcurrencyLimiter(
                            WebSocketPartition,
                            _ => new ConcurrencyLimiterOptions
                            {
                                PermitLimit = demoOptions.MaxSessions,
                                QueueLimit = 0,
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                            })
                        : RateLimitPartition.GetNoLimiter(NonWebSocketPartition));
                options.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;
                options.OnRejected = static async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode =
                        StatusCodes.Status503ServiceUnavailable;
                    await context.HttpContext.Response.WriteAsync(
                        RejectionMessage,
                        cancellationToken);
                };

                options.AddPolicy(
                    PolicyName,
                    new DemoClientSessionRateLimiterPolicy(
                        demoOptions.MaxSessionsPerClient));
            });

        return services;
    }
}
