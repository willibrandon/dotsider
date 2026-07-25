using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;

namespace Dotsider.Website.Tests;

/// <summary>
/// Verifies validation and normalization of demo security settings.
/// </summary>
/// <param name="testContext">The context for the current test.</param>
[TestClass]
public sealed class DemoOptionsTests(TestContext testContext)
{
    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies that invalid per-client limits fail validation.
    /// </summary>
    /// <param name="maxSessions">The configured global limit.</param>
    /// <param name="maxSessionsPerClient">The configured per-client limit.</param>
    /// <param name="expectedFailure">The expected validation failure.</param>
    [TestMethod]
    [DataRow(10, 0, "Demo:MaxSessionsPerClient must be greater than zero.")]
    [DataRow(10, -1, "Demo:MaxSessionsPerClient must be greater than zero.")]
    [DataRow(
        2,
        3,
        "Demo:MaxSessionsPerClient must not exceed Demo:MaxSessions.")]
    public void Validate_InvalidPerClientLimit_Fails(
        int maxSessions,
        int maxSessionsPerClient,
        string expectedFailure)
    {
        var options = new DemoOptions
        {
            MaxSessions = maxSessions,
            MaxSessionsPerClient = maxSessionsPerClient
        };

        var result = Validate(options);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.Failures);
        Assert.Contains(expectedFailure, result.Failures);
    }

    /// <summary>
    /// Verifies that malformed or non-origin values fail validation.
    /// </summary>
    /// <param name="origin">The invalid configured origin.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("ftp://example.com")]
    [DataRow("https://example.com/#fragment")]
    [DataRow("https://example.com/?query=true")]
    [DataRow("https://example.com/path")]
    [DataRow("https://user@example.com")]
    [DataRow("not an origin")]
    [DataRow("null")]
    public void Validate_InvalidOrigin_Fails(string origin)
    {
        var options = new DemoOptions
        {
            AllowedOrigins = [origin]
        };

        var result = Validate(options);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.Failures);
        Assert.Contains("Demo:AllowedOrigins", Assert.ContainsSingle(result.Failures));
    }

    /// <summary>
    /// Verifies that an empty origin collection fails validation.
    /// </summary>
    [TestMethod]
    public void Validate_NoOrigins_Fails()
    {
        var options = new DemoOptions
        {
            AllowedOrigins = []
        };

        var result = Validate(options);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.Failures);
        Assert.Contains(
            "Demo:AllowedOrigins must contain at least one origin.",
            result.Failures);
    }

    /// <summary>
    /// Verifies that wildcard mode cannot be combined with explicit origins.
    /// </summary>
    [TestMethod]
    public void Validate_WildcardAndExplicitOrigin_Fails()
    {
        var options = new DemoOptions
        {
            AllowedOrigins = ["*", "https://dotsider.dev"]
        };

        var result = Validate(options);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.Failures);
        Assert.Contains(
            "Demo:AllowedOrigins cannot combine '*' with explicit origins.",
            result.Failures);
    }

    /// <summary>
    /// Verifies malformed trusted-proxy entries fail validation.
    /// </summary>
    /// <param name="trustedProxy">The invalid proxy address.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("localhost")]
    [DataRow("10.0.0.0/8")]
    [DataRow("not-an-address")]
    public void Validate_InvalidTrustedProxy_Fails(string trustedProxy)
    {
        var options = new DemoOptions
        {
            AllowedOrigins = ["*"],
            TrustedProxies = [trustedProxy]
        };

        var result = Validate(options);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.Failures);
        Assert.Contains(
            "Demo:TrustedProxies",
            Assert.ContainsSingle(result.Failures));
    }

    /// <summary>
    /// Verifies an empty trusted-proxy list is a valid fail-closed configuration.
    /// </summary>
    [TestMethod]
    public void Validate_EmptyTrustedProxyList_Succeeds()
    {
        var options = new DemoOptions
        {
            AllowedOrigins = ["*"],
            TrustedProxies = []
        };

        var result = Validate(options);

        Assert.IsTrue(result.Succeeded);
    }

    /// <summary>
    /// Verifies a null trusted-proxy collection produces a validation failure.
    /// </summary>
    [TestMethod]
    public void Validate_NullTrustedProxyList_Fails()
    {
        var options = new DemoOptions
        {
            AllowedOrigins = ["*"],
            TrustedProxies = null!
        };

        var result = Validate(options);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNotNull(result.Failures);
        Assert.Contains(
            "Demo:TrustedProxies must be an array of exact IP addresses.",
            Assert.ContainsSingle(result.Failures));
    }

    /// <summary>
    /// Verifies that equivalent explicit origins are normalized and deduplicated.
    /// </summary>
    [TestMethod]
    public void Create_EquivalentOrigins_NormalizesAndDeduplicates()
    {
        var policy = DemoOriginPolicy.Create(
            [" HTTPS://DOTSIDER.DEV:443/ ", "https://dotsider.dev"]);

        Assert.IsFalse(policy.AllowsAnyOrigin);
        Assert.HasCount(1, policy.AllowedOrigins);
        Assert.AreEqual("https://dotsider.dev", policy.AllowedOrigins[0]);
    }

    /// <summary>
    /// Verifies that HTTP localhost origins with explicit ports remain valid.
    /// </summary>
    [TestMethod]
    public void Create_LocalDevelopmentOrigin_PreservesPort()
    {
        var policy = DemoOriginPolicy.Create(["http://localhost:4321"]);

        Assert.IsFalse(policy.AllowsAnyOrigin);
        Assert.HasCount(1, policy.AllowedOrigins);
        Assert.AreEqual("http://localhost:4321", policy.AllowedOrigins[0]);
    }

    /// <summary>
    /// Verifies that wildcard mode remains available for local development.
    /// </summary>
    [TestMethod]
    public void Create_Wildcard_AllowsAnyOrigin()
    {
        var policy = DemoOriginPolicy.Create(["*"]);

        Assert.IsTrue(policy.AllowsAnyOrigin);
        Assert.HasCount(1, policy.AllowedOrigins);
        Assert.AreEqual("*", policy.AllowedOrigins[0]);
    }

    /// <summary>
    /// Verifies that a deployment override replaces the development wildcard by index.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task StartAsync_ExplicitDeploymentOrigin_ReplacesDefaultWildcard()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:AllowedOrigins:0"] = "*"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:AllowedOrigins:0"] = "https://dotsider.dev"
            })
            .Build();
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddDemoOptions(configuration))
            .Build();

        await host.StartAsync(_testContext.CancellationToken);
        var policy = host.Services.GetRequiredService<DemoOriginPolicy>();

        Assert.IsFalse(policy.AllowsAnyOrigin);
        Assert.HasCount(1, policy.AllowedOrigins);
        Assert.AreEqual("https://dotsider.dev", policy.AllowedOrigins[0]);
    }

    /// <summary>
    /// Verifies that options validation runs when the host starts.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task StartAsync_InvalidConfiguration_FailsBeforeServingRequests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:MaxSessions"] = "2",
                ["Demo:MaxSessionsPerClient"] = "3"
            })
            .Build();
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddDemoOptions(configuration))
            .Build();

        var exception = await Assert.ThrowsExactlyAsync<OptionsValidationException>(
            () => host.StartAsync(_testContext.CancellationToken));

        Assert.Contains(
            "Demo:MaxSessionsPerClient must not exceed Demo:MaxSessions.",
            exception.Failures);
    }

    /// <summary>
    /// Verifies configured trusted proxies replace the loopback defaults.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task StartAsync_ConfiguredTrustedProxy_ReplacesDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:TrustedProxies:0"] = "127.0.0.1",
                ["Demo:TrustedProxies:1"] = "::1"
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:TrustedProxies:0"] = "10.0.0.5"
            })
            .Build();
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddDemoOptions(configuration))
            .Build();

        await host.StartAsync(_testContext.CancellationToken);
        var options = host.Services
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        Assert.HasCount(1, options.KnownProxies);
        Assert.Contains(IPAddress.Parse("10.0.0.5"), options.KnownProxies);
        Assert.DoesNotContain(IPAddress.Loopback, options.KnownProxies);
        Assert.DoesNotContain(IPAddress.IPv6Loopback, options.KnownProxies);
    }

    /// <summary>
    /// Verifies malformed trusted-proxy configuration fails when the host starts.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task StartAsync_InvalidTrustedProxy_FailsBeforeServingRequests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Demo:TrustedProxies:0"] = "10.0.0.0/8"
            })
            .Build();
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddDemoOptions(configuration))
            .Build();

        var exception = await Assert.ThrowsExactlyAsync<OptionsValidationException>(
            () => host.StartAsync(_testContext.CancellationToken));

        Assert.Contains("Demo:TrustedProxies", Assert.ContainsSingle(exception.Failures));
    }

    private static ValidateOptionsResult Validate(DemoOptions options)
    {
        var validator = (IValidateOptions<DemoOptions>)new DemoOptionsValidator();
        return validator.Validate(name: null, options);
    }
}
