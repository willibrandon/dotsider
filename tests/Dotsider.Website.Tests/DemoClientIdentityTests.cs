using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;

namespace Dotsider.Website.Tests;

/// <summary>
/// Verifies trusted proxy processing and client partition identities.
/// </summary>
/// <param name="testContext">The context for the current test.</param>
[TestClass]
public sealed class DemoClientIdentityTests(TestContext testContext)
{
    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies that an untrusted peer cannot replace its address with a forwarded value.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ForwardedFor_UntrustedPeer_IsIgnored()
    {
        using var host = await StartHostAsync(_testContext.CancellationToken);

        var identity = await SendIdentityAsync(
            host.GetTestServer(),
            IPAddress.Parse("203.0.113.10"),
            "198.51.100.10",
            _testContext.CancellationToken);

        Assert.AreEqual("203.0.113.10", identity);
    }

    /// <summary>
    /// Verifies that the same-host proxy can supply the original client address.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ForwardedFor_LoopbackProxy_IsApplied()
    {
        using var host = await StartHostAsync(_testContext.CancellationToken);

        var identity = await SendIdentityAsync(
            host.GetTestServer(),
            IPAddress.Loopback,
            "198.51.100.20",
            _testContext.CancellationToken);

        Assert.AreEqual("198.51.100.20", identity);
    }

    /// <summary>
    /// Verifies that only the nearest forwarded hop is consumed.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ForwardedFor_MultipleValues_UsesNearestHopOnly()
    {
        using var host = await StartHostAsync(_testContext.CancellationToken);

        var identity = await SendIdentityAsync(
            host.GetTestServer(),
            IPAddress.Loopback,
            "198.51.100.30, 203.0.113.30",
            _testContext.CancellationToken);

        Assert.AreEqual("203.0.113.30", identity);
    }

    /// <summary>
    /// Verifies that malformed forwarded addresses do not replace the proxy address.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task ForwardedFor_MalformedValue_IsIgnored()
    {
        using var host = await StartHostAsync(_testContext.CancellationToken);

        var identity = await SendIdentityAsync(
            host.GetTestServer(),
            IPAddress.Loopback,
            "not-an-address",
            _testContext.CancellationToken);

        Assert.AreEqual(IPAddress.Loopback.ToString(), identity);
    }

    /// <summary>
    /// Verifies that IPv4 and mapped IPv6 addresses share one partition identity.
    /// </summary>
    [TestMethod]
    public void GetPartitionKey_IPv4MappedAddress_NormalizesToIPv4()
    {
        var ipv4Context = new DefaultHttpContext();
        ipv4Context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.40");
        var mappedContext = new DefaultHttpContext();
        mappedContext.Connection.RemoteIpAddress =
            IPAddress.Parse("::ffff:192.0.2.40");

        var ipv4Identity = DemoClientIdentity.GetPartitionKey(ipv4Context);
        var mappedIdentity = DemoClientIdentity.GetPartitionKey(mappedContext);

        Assert.AreEqual(ipv4Identity, mappedIdentity);
        Assert.AreEqual("192.0.2.40", mappedIdentity);
    }

    /// <summary>
    /// Verifies that requests without a transport address share a bounded fallback partition.
    /// </summary>
    [TestMethod]
    public void GetPartitionKey_MissingAddress_UsesSharedFallback()
    {
        var firstContext = new DefaultHttpContext();
        var secondContext = new DefaultHttpContext();

        var firstIdentity = DemoClientIdentity.GetPartitionKey(firstContext);
        var secondIdentity = DemoClientIdentity.GetPartitionKey(secondContext);

        Assert.AreEqual("unknown", firstIdentity);
        Assert.AreEqual(firstIdentity, secondIdentity);
    }

    private static async Task<string> SendIdentityAsync(
        TestServer server,
        IPAddress remoteAddress,
        string forwardedFor,
        CancellationToken cancellationToken)
    {
        var context = await server.SendAsync(
            context =>
            {
                context.Connection.RemoteIpAddress = remoteAddress;
                context.Request.Headers["X-Forwarded-For"] = forwardedFor;
                context.Request.Method = HttpMethods.Get;
                context.Request.Path = "/identity";
            },
            cancellationToken);

        return context.Response.Headers["X-Test-Client-Identity"].ToString();
    }

    private static async Task<IHost> StartHostAsync(CancellationToken cancellationToken)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddDemoOptions(configuration);
                    services.AddRouting();
                });
                webHost.Configure(app =>
                {
                    app.UseForwardedHeaders();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/identity", context =>
                        {
                            context.Response.Headers["X-Test-Client-Identity"] =
                                DemoClientIdentity.GetPartitionKey(context);
                            return Task.CompletedTask;
                        });
                    });
                });
            })
            .StartAsync(cancellationToken);

        return host;
    }
}
