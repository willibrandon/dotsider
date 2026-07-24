using System.Net;
using System.Net.WebSockets;

namespace Dotsider.Website;

internal sealed class DemoWebSocketSessionHandler
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger _logger;
    private readonly int _maxSessions;
    private readonly Func<WebSocket, CancellationToken, Task> _runSession;
    private readonly TimeSpan _sessionTimeout;
    private int _activeSessions;

    internal DemoWebSocketSessionHandler(
        IHostApplicationLifetime applicationLifetime,
        ILogger logger,
        int maxSessions,
        TimeSpan sessionTimeout,
        Func<WebSocket, CancellationToken, Task> runSession)
    {
        _applicationLifetime = applicationLifetime;
        _logger = logger;
        _maxSessions = maxSessions;
        _sessionTimeout = sessionTimeout;
        _runSession = runSession;
    }

    internal int ActiveSessions => Volatile.Read(ref _activeSessions);

    internal async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket connections only");
            return;
        }

        var ipAddress = GetClientIpAddress(context);
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var sessionId = Guid.NewGuid().ToString("N")[..12];

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        var sessionStart = DateTimeOffset.UtcNow;
        var activeSessions = Interlocked.Increment(ref _activeSessions);

        try
        {
            Log.AuditConnect(_logger, sessionId, ipAddress, userAgent);
            Log.SessionStarted(_logger, activeSessions, _maxSessions);

            using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted,
                _applicationLifetime.ApplicationStopping);
            sessionCts.CancelAfter(_sessionTimeout);

            await _runSession(webSocket, sessionCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
        catch (Exception exception)
        {
            Log.SessionError(_logger, exception);
        }
        finally
        {
            var remainingSessions = Interlocked.Decrement(ref _activeSessions);
            Log.AuditDisconnect(
                _logger,
                sessionId,
                ipAddress,
                (DateTimeOffset.UtcNow - sessionStart).TotalSeconds);
            Log.SessionEnded(_logger, remainingSessions, _maxSessions);
        }
    }

    private static IPAddress GetClientIpAddress(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress ?? IPAddress.Loopback;
        if (!context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
            return ipAddress;

        var firstAddress = forwarded.ToString().Split(',', StringSplitOptions.TrimEntries)[0];
        return IPAddress.TryParse(firstAddress, out var parsedAddress)
            ? parsedAddress
            : ipAddress;
    }
}
