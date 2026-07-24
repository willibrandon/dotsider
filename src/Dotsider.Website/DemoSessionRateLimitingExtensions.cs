using System.Threading.RateLimiting;

namespace Dotsider.Website;

internal static class DemoSessionRateLimitingExtensions
{
    internal const string PolicyName = "demo-sessions";

    private const string NonWebSocketPartition = "non-websocket";
    private const string RejectionMessage = "Too many active sessions";
    private const string WebSocketPartition = "websocket";

    internal static IServiceCollection AddDemoSessionRateLimiting(
        this IServiceCollection services,
        int maxSessions)
    {
        if (maxSessions <= 0)
            throw new InvalidOperationException("Demo:MaxSessions must be greater than zero.");

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;
            options.OnRejected = static async (context, cancellationToken) =>
            {
                await context.HttpContext.Response.WriteAsync(RejectionMessage, cancellationToken);
            };

            options.AddPolicy<string>(PolicyName, context =>
                context.WebSockets.IsWebSocketRequest
                    ? RateLimitPartition.GetConcurrencyLimiter(
                        WebSocketPartition,
                        _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = maxSessions,
                            QueueLimit = 0,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                        })
                    : RateLimitPartition.GetNoLimiter(NonWebSocketPartition));
        });
    }
}
