using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;

namespace Dotsider.Website.Tests;

/// <summary>
/// Verifies the public readiness endpoint contract.
/// </summary>
/// <param name="testContext">The context for the current test.</param>
[TestClass]
public sealed class DemoHealthEndpointTests(TestContext testContext)
{
    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies health exposes readiness without operational session details.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task Health_ReturnsOnlyReadinessStatus()
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(static services => services.AddRouting());
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(static endpoints => endpoints.MapDemoHealth());
                });
            })
            .StartAsync(_testContext.CancellationToken);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync(
            "/health",
            _testContext.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(
            _testContext.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateObject().ToArray();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(response.Content.Headers.ContentType);
        Assert.AreEqual(
            "application/json",
            response.Content.Headers.ContentType.MediaType);
        Assert.AreEqual("""{"status":"ok"}""", json);
        Assert.HasCount(1, properties);
        Assert.AreEqual("status", properties[0].Name);
        Assert.AreEqual("ok", properties[0].Value.GetString());
    }
}
