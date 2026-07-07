using Dotsider;
using Dotsider.Infrastructure;
using Dotsider.Website;
using Hex1b;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();

var app = builder.Build();

var config = app.Configuration;
var logger = app.Logger;
var maxSessions = config.GetValue("Demo:MaxSessions", 50);
var sessionTimeout = TimeSpan.FromMinutes(config.GetValue("Demo:SessionTimeoutMinutes", 10));
var sampleAssembly = config.GetValue<string>("Demo:SampleAssembly") ?? "sample.dll";
var allowedOrigins = config.GetSection("Demo:AllowedOrigins").Get<string[]>() ?? ["*"];

var activeSessions = 0;

app.UseCors(policy =>
{
    if (allowedOrigins is ["*"])
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
});

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    activeSessions,
    maxSessions,
}));

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connections only");
        return;
    }

    // Resolve client IP (trust X-Forwarded-For from Caddy)
    var ip = context.Connection.RemoteIpAddress ?? IPAddress.Loopback;
    if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
    {
        var first = forwarded.ToString().Split(',', StringSplitOptions.TrimEntries)[0];
        if (IPAddress.TryParse(first, out var parsed))
            ip = parsed;
    }

    var userAgent = context.Request.Headers.UserAgent.ToString();
    var sessionId = Guid.NewGuid().ToString("N")[..12];

    // Global session cap
    if (Interlocked.Increment(ref activeSessions) > maxSessions)
    {
        Interlocked.Decrement(ref activeSessions);
        context.Response.StatusCode = 503;
        await context.Response.WriteAsync("Too many active sessions");
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();

    Log.AuditConnect(logger, sessionId, ip, userAgent);
    Log.SessionStarted(logger, activeSessions, maxSessions);

    var sessionStart = DateTimeOffset.UtcNow;

    var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(
        context.RequestAborted, lifetime.ApplicationStopping);
    sessionCts.CancelAfter(sessionTimeout);

    try
    {
        await RunDotsiderSession(ws, sessionCts.Token);
    }
    catch (OperationCanceledException) { }
    catch (WebSocketException) { }
    catch (Exception ex) { Log.SessionError(logger, ex); }
    finally
    {
        Interlocked.Decrement(ref activeSessions);
        Log.AuditDisconnect(logger, sessionId, ip, (DateTimeOffset.UtcNow - sessionStart).TotalSeconds);
        Log.SessionEnded(logger, activeSessions, maxSessions);
        sessionCts.Dispose();
    }
});

app.Run();

async Task RunDotsiderSession(WebSocket ws, CancellationToken ct)
{
    var filePath = Path.GetFullPath(sampleAssembly);

    await using var wsAdapter = new WebSocketPresentationAdapter(ws, 120, 36, enableMouse: true);
    await using var presentation = new EscapeTimeoutPresentationAdapter(wsAdapter);

    var workload = new Hex1bAppWorkloadAdapter(presentation.Capabilities);

    var pendingMutations = new ConcurrentQueue<Action<DotsiderState>>();

    var terminalOptions = new Hex1bTerminalOptions
    {
        PresentationAdapter = presentation,
        WorkloadAdapter = workload
    };

    using var terminal = new Hex1bTerminal(terminalOptions);
    presentation.Terminal = terminal;

    var appOptions = new Hex1bAppOptions
    {
        WorkloadAdapter = workload,
        Theme = DotsiderTheme.Create(),
        EnableMouse = true,
        EnableInputCoalescing = false
    };

    DotsiderState? capturedState = null;
    DotsiderApp? dotsiderApp = null;
    Hex1bApp? hex1bApp = null;

    hex1bApp = new Hex1bApp(ctx =>
    {
        capturedState ??= new DotsiderState(hex1bApp!, filePath, pendingMutations);
        dotsiderApp ??= new DotsiderApp(capturedState);
        return dotsiderApp.Build(ctx);
    }, appOptions);

    try
    {
        await hex1bApp.RunAsync(ct);
    }
    finally
    {
        hex1bApp.Dispose();
    }
}
