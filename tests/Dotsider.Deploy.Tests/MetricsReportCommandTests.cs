using Dotsider.DeployHost;
using Microsoft.Extensions.Time.Testing;

namespace Dotsider.Deploy.Tests;

/// <summary>
/// Verifies Prometheus query encoding and the established metrics log representation.
/// Successful values and unavailable metrics are exercised independently.
/// Tests use an isolated log file and deterministic UTC time.
/// </summary>
[TestClass]
public sealed class MetricsReportCommandTests
{
    /// <summary>
    /// Verifies all five queries and the precise successful log shape.
    /// A deterministic clock makes the UTC prefix stable.
    /// Request capture confirms one HTTP request per metric.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_QueriesFiveMetricsAndAppendsEstablishedFormat()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string logPath = Path.Combine(directory, "metrics.log");
            var handler = new StubHttpMessageHandler();
            using var client = new HttpClient(handler);
            var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 7, 12, 34, 56, TimeSpan.Zero));
            var command = new MetricsReportCommand(client, time, "http://localhost:9090", logPath);

            int exitCode = await command.RunAsync(TestContext.CancellationToken);

            Assert.AreEqual(0, exitCode);
            Assert.HasCount(5, handler.RequestUris);
            Assert.IsTrue(handler.RequestUris.All(static uri => uri.Query.StartsWith("?query=", StringComparison.Ordinal)));
            Assert.AreEqual(
                $"2026-08-07T12:34:56Z | req/s=1.25     err/s=1.25     p95=1.25       inflight=1.25 upstream_healthy=1.25{Environment.NewLine}",
                await File.ReadAllTextAsync(logPath, TestContext.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Verifies graceful output when Prometheus is unavailable.
    /// Every metric receives the established N/A placeholder.
    /// The command still completes successfully and writes a line.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_UnavailablePrometheusWritesNAForEveryMetric()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string logPath = Path.Combine(directory, "metrics.log");
            var handler = new StubHttpMessageHandler(
                static _ => new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
            using var client = new HttpClient(handler);
            var command = new MetricsReportCommand(client, TimeProvider.System, "http://localhost:9090", logPath);

            int exitCode = await command.RunAsync(TestContext.CancellationToken);
            string line = await File.ReadAllTextAsync(logPath, TestContext.CancellationToken);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(5, line.Split("N/A", StringSplitOptions.None).Length - 1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "dotsider-deploy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Gets or sets the current MSTest execution context.
    /// The cancellation token is passed to asynchronous operations.
    /// MSTest supplies the value before each test begins.
    /// </summary>
    public TestContext TestContext { get; set; }
}
