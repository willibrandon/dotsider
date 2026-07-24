using Dotsider;
using Dotsider.Infrastructure;
using Dotsider.Website;
using Hex1b;
using System.Collections.Concurrent;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

var maxSessions = builder.Configuration.GetValue("Demo:MaxSessions", 50);
var sessionTimeout = TimeSpan.FromMinutes(builder.Configuration.GetValue("Demo:SessionTimeoutMinutes", 10));
var sampleAssembly = builder.Configuration.GetValue<string>("Demo:SampleAssembly") ?? "sample.dll";
var allowedOrigins = builder.Configuration.GetSection("Demo:AllowedOrigins").Get<string[]>() ?? ["*"];

builder.Services.AddCors();
builder.Services.AddDemoSessionRateLimiting(maxSessions);

var app = builder.Build();
var logger = app.Logger;

app.UseCors(policy =>
{
    if (allowedOrigins is ["*"])
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
});

app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.UseRateLimiter();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var sessionHandler = new DemoWebSocketSessionHandler(
    lifetime,
    logger,
    maxSessions,
    sessionTimeout,
    RunDotsiderSession);

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    activeSessions = sessionHandler.ActiveSessions,
    maxSessions,
}));

app.Map("/ws", sessionHandler.HandleAsync)
    .RequireRateLimiting(DemoSessionRateLimitingExtensions.PolicyName);

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
