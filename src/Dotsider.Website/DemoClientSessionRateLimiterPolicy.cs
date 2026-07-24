using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Dotsider.Website;

internal sealed class DemoClientSessionRateLimiterPolicy : IRateLimiterPolicy<string>
{
    private const string NonWebSocketPartition = "non-websocket";
    private const string RejectionMessage = "Too many active sessions for this client";
    private readonly int _permitLimit;

    internal DemoClientSessionRateLimiterPolicy(int permitLimit)
    {
        _permitLimit = permitLimit;
    }

    Func<OnRejectedContext, CancellationToken, ValueTask>?
        IRateLimiterPolicy<string>.OnRejected => OnRejectedAsync;

    RateLimitPartition<string> IRateLimiterPolicy<string>.GetPartition(
        HttpContext httpContext)
    {
        return httpContext.WebSockets.IsWebSocketRequest
            ? RateLimitPartition.GetConcurrencyLimiter(
                DemoClientIdentity.GetPartitionKey(httpContext),
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = _permitLimit,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                })
            : RateLimitPartition.GetNoLimiter(NonWebSocketPartition);
    }

    private static async ValueTask OnRejectedAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            RejectionMessage,
            cancellationToken);
    }
}
