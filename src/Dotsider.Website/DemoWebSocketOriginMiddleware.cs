using Microsoft.Extensions.Primitives;

namespace Dotsider.Website;

/// <summary>
/// Rejects WebSocket upgrade requests that omit a required origin.
/// </summary>
/// <param name="next">The next middleware in the request pipeline.</param>
/// <param name="requireOrigin">
/// A value indicating whether WebSocket requests must supply an origin.
/// </param>
internal sealed class DemoWebSocketOriginMiddleware(
    RequestDelegate next,
    bool requireOrigin)
{
    private static readonly PathString WebSocketPath = new("/ws");
    private readonly RequestDelegate _next = next;
    private readonly bool _requireOrigin = requireOrigin;

    /// <summary>
    /// Rejects WebSocket upgrade requests that omit a required origin.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that represents the middleware operation.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        if (_requireOrigin &&
            context.Request.Path == WebSocketPath &&
            context.WebSockets.IsWebSocketRequest &&
            StringValues.IsNullOrEmpty(context.Request.Headers.Origin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return _next(context);
    }
}
