using Dotsider;
using Dotsider.Infrastructure;
using Dotsider.Website;
using Hex1b;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();
builder.Services.AddDemoOptions(builder.Configuration);
builder.Services.AddDemoSessionRateLimiting();

var app = builder.Build();
var demoOptions = app.Services.GetRequiredService<IOptions<DemoOptions>>().Value;
var logger = app.Logger;
var maxSessions = demoOptions.MaxSessions;
var originPolicy = app.Services.GetRequiredService<DemoOriginPolicy>();
var sampleAssembly = demoOptions.SampleAssembly;
var sessionTimeout = TimeSpan.FromMinutes(demoOptions.SessionTimeoutMinutes);

app.UseForwardedHeaders();
app.UseCors(policy =>
{
    if (originPolicy.AllowsAnyOrigin)
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else
        policy.WithOrigins(originPolicy.AllowedOrigins).AllowAnyHeader().AllowAnyMethod();
});

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
};
foreach (var origin in originPolicy.AllowedOrigins)
    webSocketOptions.AllowedOrigins.Add(origin);

app.UseWebSockets(webSocketOptions);
app.UseMiddleware<DemoWebSocketOriginMiddleware>(!originPolicy.AllowsAnyOrigin);
app.UseRateLimiter();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var sessionHandler = new DemoWebSocketSessionHandler(
    lifetime,
    logger,
    maxSessions,
    sessionTimeout,
    RunDotsiderSession);

app.MapDemoHealth();

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
