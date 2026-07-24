using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Net;
using System.Net.WebSockets;

namespace Dotsider.Website.Tests;

/// <summary>
/// Verifies WebSocket origin enforcement at the production middleware boundary.
/// </summary>
/// <param name="testContext">The context for the current test.</param>
[TestClass]
public sealed class DemoWebSocketOriginTests(TestContext testContext)
{
    private const string AllowedOrigin = "https://dotsider.dev";
    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies that foreign, multiple, and missing origins are rejected before admission.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ExplicitOrigins_InvalidRequests_AreRejectedBeforeAdmission()
    {
        var finishSession = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionInvocations = 0;

        using var host = await StartHostAsync(
            [AllowedOrigin],
            async (_, cancellationToken) =>
            {
                Interlocked.Increment(ref sessionInvocations);
                sessionStarted.SetResult();
                await finishSession.Task.WaitAsync(cancellationToken);
            },
            _testContext.CancellationToken);
        var server = host.GetTestServer();

        var foreignContext = await SendUpgradeRequestAsync(
            server,
            ["https://attacker.example"],
            _testContext.CancellationToken);
        var missingContext = await SendUpgradeRequestAsync(
            server,
            origins: null,
            _testContext.CancellationToken);
        var multipleContext = await SendUpgradeRequestAsync(
            server,
            [AllowedOrigin, "https://attacker.example"],
            _testContext.CancellationToken);
        var wrongSchemeContext = await SendUpgradeRequestAsync(
            server,
            ["http://dotsider.dev"],
            _testContext.CancellationToken);
        var wrongPortContext = await SendUpgradeRequestAsync(
            server,
            ["https://dotsider.dev:444"],
            _testContext.CancellationToken);

        Assert.AreEqual(StatusCodes.Status403Forbidden, foreignContext.Response.StatusCode);
        Assert.AreEqual(StatusCodes.Status403Forbidden, missingContext.Response.StatusCode);
        Assert.AreEqual(StatusCodes.Status403Forbidden, multipleContext.Response.StatusCode);
        Assert.AreEqual(StatusCodes.Status403Forbidden, wrongSchemeContext.Response.StatusCode);
        Assert.AreEqual(StatusCodes.Status403Forbidden, wrongPortContext.Response.StatusCode);
        Assert.AreEqual(0, Volatile.Read(ref sessionInvocations));

        var webSocketClient = server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest =
            request => request.Headers.Origin = AllowedOrigin;
        using var webSocket = await webSocketClient.ConnectAsync(
            new Uri("ws://localhost/ws"),
            _testContext.CancellationToken);

        await sessionStarted.Task.WaitAsync(_testContext.CancellationToken);
        Assert.AreEqual(1, Volatile.Read(ref sessionInvocations));

        finishSession.SetResult();
    }

    /// <summary>
    /// Verifies that wildcard development mode accepts missing and arbitrary origins.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task WildcardOrigin_MissingAndArbitraryOrigins_AreAccepted()
    {
        var sessionInvocations = 0;
        using var invoked = new SemaphoreSlim(0);
        using var host = await StartHostAsync(
            ["*"],
            (_, _) =>
            {
                Interlocked.Increment(ref sessionInvocations);
                invoked.Release();
                return Task.CompletedTask;
            },
            _testContext.CancellationToken,
            maxSessions: 2);
        var server = host.GetTestServer();

        var missingOriginClient = server.CreateWebSocketClient();
        using var missingOriginSocket = await missingOriginClient.ConnectAsync(
            new Uri("ws://localhost/ws"),
            _testContext.CancellationToken);

        var arbitraryOriginClient = server.CreateWebSocketClient();
        arbitraryOriginClient.ConfigureRequest =
            request => request.Headers.Origin = "https://arbitrary.example";
        using var arbitraryOriginSocket = await arbitraryOriginClient.ConnectAsync(
            new Uri("ws://localhost/ws"),
            _testContext.CancellationToken);

        await invoked.WaitAsync(_testContext.CancellationToken);
        await invoked.WaitAsync(_testContext.CancellationToken);
        Assert.AreEqual(2, Volatile.Read(ref sessionInvocations));
    }

    /// <summary>
    /// Verifies that an ordinary HTTP request is not subject to WebSocket origin checks.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task NonWebSocketRequest_WithoutOrigin_RetainsBadRequestResponse()
    {
        using var host = await StartHostAsync(
            [AllowedOrigin],
            static (_, _) => Task.CompletedTask,
            _testContext.CancellationToken);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(
            "/ws",
            _testContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(
            "WebSocket connections only",
            await response.Content.ReadAsStringAsync(_testContext.CancellationToken));
    }

    private static async Task<HttpContext> SendUpgradeRequestAsync(
        TestServer server,
        string[]? origins,
        CancellationToken cancellationToken)
    {
        return await server.SendAsync(
            context =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Loopback;
                context.Features.Set<IHttpUpgradeFeature>(
                    TestUpgradableRequestFeature.Instance);
                context.Request.Headers.Connection = "Upgrade";
                if (origins is not null)
                    context.Request.Headers.Origin = new StringValues(origins);
                context.Request.Headers.SecWebSocketKey =
                    "dGhlIHNhbXBsZSBub25jZQ==";
                context.Request.Headers.SecWebSocketVersion = "13";
                context.Request.Headers.Upgrade = "websocket";
                context.Request.Method = HttpMethods.Get;
                context.Request.Path = "/ws";
            },
            cancellationToken);
    }

    private static async Task<IHost> StartHostAsync(
        string[] allowedOrigins,
        Func<WebSocket, CancellationToken, Task> runSession,
        CancellationToken cancellationToken,
        int maxSessions = 1)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Demo:MaxSessions"] = maxSessions.ToString(),
            ["Demo:MaxSessionsPerClient"] = maxSessions.ToString()
        };
        for (var index = 0; index < allowedOrigins.Length; index++)
            settings[$"Demo:AllowedOrigins:{index}"] = allowedOrigins[index];

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddDemoOptions(configuration);
                    services.AddDemoSessionRateLimiting();
                    services.AddRouting();
                });
                webHost.Configure(app =>
                {
                    var options = app.ApplicationServices
                        .GetRequiredService<
                            Microsoft.Extensions.Options.IOptions<DemoOptions>>()
                        .Value;
                    var originPolicy =
                        app.ApplicationServices.GetRequiredService<DemoOriginPolicy>();
                    var webSocketOptions = new WebSocketOptions();
                    foreach (var origin in originPolicy.AllowedOrigins)
                        webSocketOptions.AllowedOrigins.Add(origin);

                    app.UseForwardedHeaders();
                    app.UseWebSockets(webSocketOptions);
                    app.UseMiddleware<DemoWebSocketOriginMiddleware>(
                        !originPolicy.AllowsAnyOrigin);
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseEndpoints(endpoints =>
                    {
                        var handler = new DemoWebSocketSessionHandler(
                            app.ApplicationServices
                                .GetRequiredService<IHostApplicationLifetime>(),
                            app.ApplicationServices
                                .GetRequiredService<
                                    ILogger<DemoWebSocketSessionHandler>>(),
                            options.MaxSessions,
                            TimeSpan.FromMinutes(options.SessionTimeoutMinutes),
                            runSession);

                        endpoints.Map("/ws", handler.HandleAsync)
                            .RequireRateLimiting(
                                DemoSessionRateLimitingExtensions.PolicyName);
                    });
                });
            })
            .StartAsync(cancellationToken);

        return host;
    }
}
