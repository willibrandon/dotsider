using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.WebSockets;

namespace Dotsider.Website.Tests;

/// <summary>
/// Verifies the demo WebSocket concurrency limit and accepted-session lifecycle.
/// </summary>
/// <param name="testContext">The context for the current test.</param>
[TestClass]
public sealed class DemoSessionRateLimitingTests(TestContext testContext)
{
    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies that invalid configured session limits fail during service registration.
    /// </summary>
    /// <param name="maxSessions">The invalid configured session limit.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void AddDemoSessionRateLimiting_NonPositiveLimit_Throws(int maxSessions)
    {
        var services = new ServiceCollection();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => services.AddDemoSessionRateLimiting(maxSessions));

        Assert.AreEqual("Demo:MaxSessions must be greater than zero.", exception.Message);
    }

    /// <summary>
    /// Verifies that failed WebSocket acceptance never consumes a concurrency permit.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task WebSocketAcceptFailure_RepeatedAborts_ReleasePermit()
    {
        const int maxSessions = 3;
        var abortHandshake = true;
        var finishSession = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DemoWebSocketSessionHandler? sessionHandler = null;

        using var host = await StartHostAsync(
            maxSessions,
            _ => Volatile.Read(ref abortHandshake)
                ? AbortingWebSocketFeature.Instance
                : null,
            (services, endpoints) =>
            {
                sessionHandler = CreateSessionHandler(
                    services,
                    maxSessions,
                    async (_, cancellationToken) =>
                    {
                        sessionStarted.SetResult();
                        await finishSession.Task.WaitAsync(cancellationToken);
                    });

                endpoints.Map("/ws", sessionHandler.HandleAsync)
                    .RequireRateLimiting(DemoSessionRateLimitingExtensions.PolicyName);
            },
            _testContext.CancellationToken);

        Assert.IsNotNull(sessionHandler);
        using var client = host.GetTestClient();

        for (var attempt = 0; attempt < maxSessions * 2; attempt++)
        {
            await Assert.ThrowsExactlyAsync<ConnectionAbortedException>(
                () => client.GetAsync("/ws", _testContext.CancellationToken));
            Assert.AreEqual(0, sessionHandler.ActiveSessions);
        }

        Volatile.Write(ref abortHandshake, false);

        var webSocketClient = host.GetTestServer().CreateWebSocketClient();
        using var webSocket = await webSocketClient.ConnectAsync(
            new Uri("ws://localhost/ws"),
            _testContext.CancellationToken);

        await sessionStarted.Task.WaitAsync(_testContext.CancellationToken);
        Assert.AreEqual(1, sessionHandler.ActiveSessions);

        finishSession.SetResult();
        await WaitUntilAsync(
            () => sessionHandler.ActiveSessions == 0,
            _testContext.CancellationToken);
        Assert.AreEqual(0, sessionHandler.ActiveSessions);
    }

    /// <summary>
    /// Verifies that the concurrency policy admits only the configured number of WebSocket requests.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task WebSocketRequests_AtCapacity_RejectImmediatelyAndRecover()
    {
        const int excessRequests = 8;
        const int maxSessions = 3;
        var activeRequests = 0;
        var maximumActiveRequests = 0;
        using var admitted = new SemaphoreSlim(0);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DemoWebSocketSessionHandler? sessionHandler = null;

        using var host = await StartHostAsync(
            maxSessions,
            context => context.Request.Query.ContainsKey("websocket")
                ? AbortingWebSocketFeature.Instance
                : null,
            (services, endpoints) =>
            {
                sessionHandler = CreateSessionHandler(
                    services,
                    maxSessions,
                    static (_, _) => Task.CompletedTask);

                endpoints.Map(
                        "/ws",
                        async context =>
                        {
                            if (!context.WebSockets.IsWebSocketRequest)
                            {
                                await sessionHandler.HandleAsync(context);
                                return;
                            }

                            var active = Interlocked.Increment(ref activeRequests);
                            UpdateMaximum(ref maximumActiveRequests, active);
                            admitted.Release();

                            try
                            {
                                await release.Task.WaitAsync(context.RequestAborted);
                                context.Response.StatusCode = StatusCodes.Status204NoContent;
                            }
                            finally
                            {
                                Interlocked.Decrement(ref activeRequests);
                            }
                        })
                    .RequireRateLimiting(DemoSessionRateLimitingExtensions.PolicyName);
            },
            _testContext.CancellationToken);

        Assert.IsNotNull(sessionHandler);
        using var client = host.GetTestClient();
        var admittedRequests = Enumerable.Range(0, maxSessions)
            .Select(_ => client.GetAsync("/ws?websocket=true", _testContext.CancellationToken))
            .ToArray();

        for (var request = 0; request < maxSessions; request++)
            await admitted.WaitAsync(_testContext.CancellationToken);

        var rejectedRequests = await Task.WhenAll(
            Enumerable.Range(0, excessRequests)
                .Select(_ => client.GetAsync("/ws?websocket=true", _testContext.CancellationToken)));

        foreach (var response in rejectedRequests)
        {
            using (response)
            {
                Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                Assert.AreEqual(
                    "Too many active sessions",
                    await response.Content.ReadAsStringAsync(_testContext.CancellationToken));
            }
        }

        using (var nonWebSocketResponse =
               await client.GetAsync("/ws", _testContext.CancellationToken))
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, nonWebSocketResponse.StatusCode);
            Assert.AreEqual(
                "WebSocket connections only",
                await nonWebSocketResponse.Content.ReadAsStringAsync(_testContext.CancellationToken));
        }

        Assert.AreEqual(maxSessions, maximumActiveRequests);
        release.SetResult();

        var completedRequests = await Task.WhenAll(admittedRequests);
        foreach (var response in completedRequests)
        {
            using (response)
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        using var recoveredResponse =
            await client.GetAsync("/ws?websocket=true", _testContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.NoContent, recoveredResponse.StatusCode);
        Assert.AreEqual(0, Volatile.Read(ref activeRequests));
    }

    /// <summary>
    /// Verifies that health reports only accepted sessions and returns to zero after completion.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task AcceptedWebSocket_WhileSessionRuns_HealthTracksSession()
    {
        const int maxSessions = 1;
        var sessionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishSession = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DemoWebSocketSessionHandler? sessionHandler = null;

        using var host = await StartHostAsync(
            maxSessions,
            featureFactory: null,
            (services, endpoints) =>
            {
                sessionHandler = CreateSessionHandler(
                    services,
                    maxSessions,
                    async (_, cancellationToken) =>
                    {
                        sessionStarted.SetResult();
                        await finishSession.Task.WaitAsync(cancellationToken);
                    });

                endpoints.Map("/ws", sessionHandler.HandleAsync)
                    .RequireRateLimiting(DemoSessionRateLimitingExtensions.PolicyName);
                endpoints.MapGet("/health", () => Results.Ok(new
                {
                    activeSessions = sessionHandler.ActiveSessions,
                    maxSessions
                }));
            },
            _testContext.CancellationToken);

        Assert.IsNotNull(sessionHandler);
        using var httpClient = host.GetTestClient();
        var webSocketClient = host.GetTestServer().CreateWebSocketClient();
        using var webSocket = await webSocketClient.ConnectAsync(
            new Uri("ws://localhost/ws"),
            _testContext.CancellationToken);

        await sessionStarted.Task.WaitAsync(_testContext.CancellationToken);

        using (var activeResponse =
               await httpClient.GetAsync("/health", _testContext.CancellationToken))
        {
            Assert.AreEqual(HttpStatusCode.OK, activeResponse.StatusCode);
            Assert.Contains(
                "\"activeSessions\":1",
                await activeResponse.Content.ReadAsStringAsync(_testContext.CancellationToken));
        }

        finishSession.SetResult();
        await WaitUntilAsync(
            () => sessionHandler.ActiveSessions == 0,
            _testContext.CancellationToken);

        using var completedResponse =
            await httpClient.GetAsync("/health", _testContext.CancellationToken);

        Assert.AreEqual(HttpStatusCode.OK, completedResponse.StatusCode);
        Assert.Contains(
            "\"activeSessions\":0",
            await completedResponse.Content.ReadAsStringAsync(_testContext.CancellationToken));
    }

    /// <summary>
    /// Verifies that an unexpected session failure releases accepted-session state.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task SessionRunnerFailure_AfterAcceptance_ReleasesSession()
    {
        const int maxSessions = 1;
        var sessionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failSession = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DemoWebSocketSessionHandler? sessionHandler = null;

        using var host = await StartHostAsync(
            maxSessions,
            featureFactory: null,
            (services, endpoints) =>
            {
                sessionHandler = CreateSessionHandler(
                    services,
                    maxSessions,
                    async (_, cancellationToken) =>
                    {
                        sessionStarted.SetResult();
                        await failSession.Task.WaitAsync(cancellationToken);
                        throw new InvalidOperationException("Expected test failure.");
                    });

                endpoints.Map("/ws", sessionHandler.HandleAsync)
                    .RequireRateLimiting(DemoSessionRateLimitingExtensions.PolicyName);
            },
            _testContext.CancellationToken);

        Assert.IsNotNull(sessionHandler);
        var webSocketClient = host.GetTestServer().CreateWebSocketClient();
        using var webSocket = await webSocketClient.ConnectAsync(
            new Uri("ws://localhost/ws"),
            _testContext.CancellationToken);

        await sessionStarted.Task.WaitAsync(_testContext.CancellationToken);
        Assert.AreEqual(1, sessionHandler.ActiveSessions);

        failSession.SetResult();
        await WaitUntilAsync(
            () => sessionHandler.ActiveSessions == 0,
            _testContext.CancellationToken);

        Assert.AreEqual(0, sessionHandler.ActiveSessions);
    }

    private static DemoWebSocketSessionHandler CreateSessionHandler(
        IServiceProvider services,
        int maxSessions,
        Func<WebSocket, CancellationToken, Task> runSession)
    {
        return new DemoWebSocketSessionHandler(
            services.GetRequiredService<IHostApplicationLifetime>(),
            services.GetRequiredService<ILogger<DemoWebSocketSessionHandler>>(),
            maxSessions,
            TimeSpan.FromMinutes(10),
            runSession);
    }

    private static async Task<IHost> StartHostAsync(
        int maxSessions,
        Func<HttpContext, IHttpWebSocketFeature?>? featureFactory,
        Action<IServiceProvider, IEndpointRouteBuilder> configureEndpoints,
        CancellationToken cancellationToken)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddDemoSessionRateLimiting(maxSessions);
                    services.AddRouting();
                });
                webHost.Configure(app =>
                {
                    app.UseWebSockets();

                    if (featureFactory is not null)
                    {
                        app.Use((context, next) =>
                        {
                            var feature = featureFactory(context);
                            if (feature is not null)
                                context.Features.Set(feature);

                            return next(context);
                        });
                    }

                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseEndpoints(
                        endpoints => configureEndpoints(app.ApplicationServices, endpoints));
                });
            })
            .StartAsync(cancellationToken);

        return host;
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        CancellationToken cancellationToken)
    {
        while (!condition())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        var observed = Volatile.Read(ref maximum);
        while (candidate > observed)
        {
            var original = Interlocked.CompareExchange(ref maximum, candidate, observed);
            if (original == observed)
                return;

            observed = original;
        }
    }
}
