using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using Dotsider;
using Dotsider.Infrastructure;
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
    MaxRapidDisconnects = config.GetValue("Demo:Guard:MaxRapidDisconnects", 10),
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

// Per-IP session tracking. Each IP may have up to MaxConcurrentPerIp
// sessions running simultaneously (e.g. multiple devices behind one NAT).
// The semaphore serializes admission so two concurrent handshakes from
// the same IP don't race the count check.
var ipSessions = new ConcurrentDictionary<IPAddress, ConcurrentDictionary<string, (CancellationTokenSource Cts, Task Task)>>();
var ipGates = new ConcurrentDictionary<IPAddress, SemaphoreSlim>();

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

    // Serialize the admission + replacement handoff per IP.
    var gate = ipGates.GetOrAdd(ip, _ => new SemaphoreSlim(1, 1));
    await gate.WaitAsync(context.RequestAborted);
    Task? fullTask = null;
    try
    {
        var sessions = ipSessions.GetOrAdd(ip, _ => new());

        // 1. Only treat as replacement when the IP already has the
        //    maximum allowed concurrent sessions and we must evict one.
        var isReplacement = sessions.Count >= guardOptions.MaxConcurrentPerIp;

        // 2. Guard checks — before cancelling anything so a rejected
        //    replacement never tears down a healthy session.
        var rejection = guard.TryAllow(ip, userAgent, isReplacement);
        if (rejection is not null)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync($"Rate limited: {rejection}");
            return;
        }

        // 3. Global session cap.
        if (Interlocked.Increment(ref activeSessions) > maxSessions)
        {
            Interlocked.Decrement(ref activeSessions);
            guard.ReleaseSlot(ip);
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("Too many active sessions");
            return;
        }

        // 4. Only evict when actually at the concurrent limit.
        if (isReplacement)
        {
            var victim = sessions.FirstOrDefault();
            if (victim.Key is not null && sessions.TryRemove(victim.Key, out var prev))
            {
                try { await prev.Cts.CancelAsync(); } catch { }
                try { await prev.Task.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
                prev.Cts.Dispose();
            }
        }

        // 5. Create and publish the new session entry.
        var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted, lifetime.ApplicationStopping);
        sessionCts.CancelAfter(sessionTimeout);

        async Task FullSessionLifecycle()
        {
            using var ws = await context.WebSockets.AcceptWebSocketAsync();

            guard.SessionStarted(ip, sessionId, userAgent);
            Log.SessionStarted(logger, activeSessions, maxSessions);

            var sessionStart = DateTimeOffset.UtcNow;

            try
            {
                await RunDotsiderSession(ws, sessionCts.Token);
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (Exception ex) { Log.SessionError(logger, ex); }
            finally
            {
                // Release guard and global slots first so a reconnect
                // can pass TryAllow even during ipSessions cleanup.
                var duration = DateTimeOffset.UtcNow - sessionStart;
                guard.SessionEnded(ip, sessionId, duration);
                guard.ReleaseSlot(ip);
                Interlocked.Decrement(ref activeSessions);

                if (ipSessions.TryGetValue(ip, out var s))
                    s.TryRemove(sessionId, out _);

                sessionCts.Dispose();
                Log.SessionEnded(logger, activeSessions, maxSessions);
            }
        }

        fullTask = FullSessionLifecycle();
        sessions[sessionId] = (sessionCts, fullTask);
    }
    finally
    {
        // 6. Release the gate so the next reconnect can proceed.
        gate.Release();
    }

    // 7. Await the session outside the gate so the gate is not held
    //    for the entire session lifetime.
    if (fullTask is not null)
        await fullTask;
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
