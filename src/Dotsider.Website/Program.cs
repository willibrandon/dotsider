using System.Collections.Concurrent;
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
    maxSessions
}));

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connections only");
        return;
    }

    if (Interlocked.Increment(ref activeSessions) > maxSessions)
    {
        Interlocked.Decrement(ref activeSessions);
        context.Response.StatusCode = 503;
        await context.Response.WriteAsync("Too many active sessions");
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    Log.SessionStarted(logger, activeSessions, maxSessions);

    try
    {
        await RunDotsiderSession(ws, context.RequestAborted);
    }
    catch (OperationCanceledException)
    {
        // Client disconnected
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
        Interlocked.Decrement(ref activeSessions);
        Log.SessionEnded(logger, activeSessions, maxSessions);
    }
});

app.Run();

async Task RunDotsiderSession(WebSocket ws, CancellationToken ct)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(sessionTimeout);

    var filePath = Path.GetFullPath(sampleAssembly);

    await using var presentation = new WebSocketPresentationAdapter(ws, 80, 24, enableMouse: true);

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

    await hex1bApp.RunAsync(cts.Token);
}
