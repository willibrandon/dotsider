using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using Dotsider;
using Dotsider.Website;
using Hex1b;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();

var app = builder.Build();

var config = app.Configuration;
var logger = app.Logger;
var maxSessions = config.GetValue("Demo:MaxSessions", 10);
var sessionTimeout = TimeSpan.FromMinutes(config.GetValue("Demo:SessionTimeoutMinutes", 10));
var sampleAssembly = config.GetValue<string>("Demo:SampleAssembly") ?? "sample.dll";
var allowedOrigins = config.GetSection("Demo:AllowedOrigins").Get<string[]>() ?? ["*"];

var activeSessions = 0;

// Demo protection — rate limiting, circuit breaker, audit logging
var guardOptions = new DemoGuardOptions
{
    MaxConnectionsPerIpPerWindow = config.GetValue("Demo:Guard:MaxConnectionsPerIpPerWindow", 10),
    RateWindow = TimeSpan.FromSeconds(config.GetValue("Demo:Guard:RateWindowSeconds", 60)),
    MaxConcurrentPerIp = config.GetValue("Demo:Guard:MaxConcurrentPerIp", 3),
    BanDuration = TimeSpan.FromMinutes(config.GetValue("Demo:Guard:BanDurationMinutes", 15)),
    MaxBanDuration = TimeSpan.FromHours(config.GetValue("Demo:Guard:MaxBanDurationHours", 24)),
    CircuitThreshold = config.GetValue("Demo:Guard:CircuitThreshold", 50),
    CircuitWindow = TimeSpan.FromSeconds(config.GetValue("Demo:Guard:CircuitWindowSeconds", 60)),
    CircuitCooldown = TimeSpan.FromMinutes(config.GetValue("Demo:Guard:CircuitCooldownMinutes", 5)),
    SuspiciousSessionDuration = TimeSpan.FromSeconds(config.GetValue("Demo:Guard:SuspiciousSessionSeconds", 2)),
    MaxRapidDisconnects = config.GetValue("Demo:Guard:MaxRapidDisconnects", 5),
};
var guard = new DemoGuard(
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<DemoGuard>(),
    guardOptions,
    TimeProvider.System);

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
    circuitOpen = guard.IsCircuitOpen
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

    // Gate: demo guard checks
    var rejection = guard.TryAllow(ip, userAgent);
    if (rejection is not null)
    {
        context.Response.StatusCode = 429;
        await context.Response.WriteAsync($"Rate limited: {rejection}");
        return;
    }

    // TryAllow reserved a per-IP slot — ensure release on all exit paths
    try
    {
        if (Interlocked.Increment(ref activeSessions) > maxSessions)
        {
            Interlocked.Decrement(ref activeSessions);
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("Too many active sessions");
            return;
        }

        // Global slot acquired — ensure decrement on all paths from here
        try
        {
            using var ws = await context.WebSockets.AcceptWebSocketAsync();

            guard.SessionStarted(ip, sessionId, userAgent);
            Log.SessionStarted(logger, activeSessions, maxSessions);

            var sessionStart = DateTimeOffset.UtcNow;

            try
            {
                await RunDotsiderSession(ws, context.RequestAborted, lifetime.ApplicationStopping);
            }
            catch (OperationCanceledException)
            {
                // Client disconnected or host shutting down
            }
            catch (WebSocketException)
            {
                // Connection dropped
            }
            catch (Exception ex)
            {
                Log.SessionError(logger, ex);
            }
            finally
            {
                var duration = DateTimeOffset.UtcNow - sessionStart;
                guard.SessionEnded(ip, sessionId, duration);
                Log.SessionEnded(logger, activeSessions, maxSessions);
            }
        }
        finally
        {
            Interlocked.Decrement(ref activeSessions);
        }
    }
    finally
    {
        guard.ReleaseSlot(ip);
    }
});

app.Run();

async Task RunDotsiderSession(WebSocket ws, CancellationToken requestAborted, CancellationToken appStopping)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, appStopping);
    cts.CancelAfter(sessionTimeout);

    var filePath = Path.GetFullPath(sampleAssembly);

    await using var presentation = new WebSocketPresentationAdapter(ws, 120, 36, enableMouse: true);

    var workload = new Hex1bAppWorkloadAdapter(presentation.Capabilities);

    var pendingMutations = new ConcurrentQueue<Action<DotsiderState>>();

    var terminalOptions = new Hex1bTerminalOptions
    {
        PresentationAdapter = presentation,
        WorkloadAdapter = workload
    };

    using var terminal = new Hex1bTerminal(terminalOptions);

    var appOptions = new Hex1bAppOptions
    {
        WorkloadAdapter = workload,
        Theme = DotsiderTheme.Create(),
        EnableMouse = true
    };

    DotsiderState? capturedState = null;
    Hex1bApp? hex1bApp = null;

    hex1bApp = new Hex1bApp(ctx =>
    {
        capturedState ??= new DotsiderState(hex1bApp!, filePath, pendingMutations);
        var dotsiderApp = new DotsiderApp(capturedState);
        return dotsiderApp.Build(ctx);
    }, appOptions);

    try
    {
        await hex1bApp.RunAsync(cts.Token);
    }
    finally
    {
        hex1bApp.Dispose();
    }
}
