namespace Dotsider.Deploy.Tests;

/// <summary>
/// Exercises deployment against Debian 13 with real systemd, Caddy, Prometheus, and UFW.
/// The suite replaces Bats coverage with ordinary MSTest assertions and lifecycle hooks.
/// Set DOTSIDER_RUN_DEPLOY_INTEGRATION to 1 to enable the privileged Docker fixture.
/// </summary>
[TestClass]
public sealed class DeploymentContainerTests
{
    private static DockerDeployFixture? s_fixture;

    /// <summary>
    /// Builds and provisions the shared Debian deployment fixture when explicitly enabled.
    /// Normal unit-test runs do not require Docker or privileged containers.
    /// Initialization performs the real setup, activation, and health check paths.
    /// </summary>
    /// <param name="testContext">The MSTest class context.</param>
    [ClassInitialize]
    public static void Initialize(TestContext testContext)
    {
        _ = testContext;
        if (Environment.GetEnvironmentVariable("DOTSIDER_RUN_DEPLOY_INTEGRATION") != "1")
        {
            return;
        }

        s_fixture = new DockerDeployFixture(FindRepositoryRoot());
        s_fixture.Initialize();
    }

    /// <summary>
    /// Removes container resources created for the integration suite.
    /// Cleanup runs after successful and failed assertions.
    /// Docker credentials remain isolated throughout teardown.
    /// </summary>
    [ClassCleanup]
    public static void Cleanup()
    {
        s_fixture?.Dispose();
    }

    /// <summary>
    /// Verifies packages, the deployment account, directories, services, and timers.
    /// Each assertion observes the real Debian host state after provisioning.
    /// The deployed helper must be root-owned and not group or world writable.
    /// </summary>
    [TestMethod]
    public void Provision_InstallsEstablishedHostLayout()
    {
        DockerDeployFixture fixture = GetFixture();

        Assert.AreEqual("brandon", ExecRequired(fixture, "id", "-un", "brandon").Trim());
        Assert.AreEqual("root:root 755", ExecRequired(fixture, "stat", "-c", "%U:%G %a", "/usr/local/libexec/dotsider-deploy-host").Trim());
        Assert.AreEqual("active", ExecRequired(fixture, "systemctl", "is-active", "caddy").Trim());
        Assert.AreEqual("active", ExecRequired(fixture, "systemctl", "is-active", "prometheus").Trim());
        Assert.AreEqual("active", ExecRequired(fixture, "systemctl", "is-active", "caddy-report.timer").Trim());
        Assert.AreEqual("active", ExecRequired(fixture, "systemctl", "is-active", "integrity-check.timer").Trim());
        Assert.AreEqual("enabled", ExecRequired(fixture, "systemctl", "is-enabled", "dotsider-website").Trim());
        Assert.AreEqual("brandon:brandon", ExecRequired(fixture, "stat", "-c", "%U:%G", "/var/www/dotsider-docs").Trim());
        Assert.AreEqual("brandon:brandon", ExecRequired(fixture, "stat", "-c", "%U:%G", "/opt/dotsider-website").Trim());
        Assert.AreEqual("700", ExecRequired(fixture, "stat", "-c", "%a", "/home/brandon/.ssh").Trim());
        Assert.AreEqual("600", ExecRequired(fixture, "stat", "-c", "%a", "/home/brandon/.ssh/authorized_keys").Trim());
        Assert.AreEqual(0, fixture.Exec("sudo", "-u", "brandon", "sudo", "-n", "true").ExitCode);
        Assert.AreNotEqual(0, fixture.Exec("which", "dotnet").ExitCode);
        foreach (string package in new[] { "caddy", "prometheus", "rsync", "ufw", "libicu76" })
        {
            Assert.Contains(
                "install ok installed",
                ExecRequired(fixture, "dpkg-query", "-W", "-f=${Status}", package));
        }

        string firewall = ExecRequired(fixture, "ufw", "status");
        Assert.Contains("22/tcp", firewall);
        Assert.Contains("80/tcp", firewall);
        Assert.Contains("443/tcp", firewall);
    }

    /// <summary>
    /// Compares every installed configuration file with its checked-in source.
    /// Caddy and Prometheus validators run against the installed files.
    /// This prevents the embedded manifest and repository assets from drifting.
    /// </summary>
    [TestMethod]
    public void Provision_InstallsAuthoritativeConfigurationExactly()
    {
        DockerDeployFixture fixture = GetFixture();
        string repositoryRoot = FindRepositoryRoot();
        (string Source, string Destination)[] files =
        [
            ("Caddyfile", "/etc/caddy/Caddyfile"),
            ("prometheus.yml", "/etc/prometheus/prometheus.yml"),
            ("dotsider-website.service", "/etc/systemd/system/dotsider-website.service"),
            ("caddy-report.service", "/etc/systemd/system/caddy-report.service"),
            ("caddy-report.timer", "/etc/systemd/system/caddy-report.timer"),
            ("integrity-check.service", "/etc/systemd/system/integrity-check.service"),
            ("integrity-check.timer", "/etc/systemd/system/integrity-check.timer"),
            ("caddy-metrics-logrotate", "/etc/logrotate.d/caddy-metrics"),
        ];
        foreach ((string source, string destination) in files)
        {
            Assert.AreEqual(
                File.ReadAllText(Path.Combine(repositoryRoot, "deploy", source)),
                ExecRequired(fixture, "cat", destination));
            Assert.AreEqual("root:root 644", ExecRequired(fixture, "stat", "-c", "%U:%G %a", destination).Trim());
        }

        _ = ExecRequired(fixture, "caddy", "validate", "--config", "/etc/caddy/Caddyfile");
        _ = ExecRequired(fixture, "promtool", "check", "config", "/etc/prometheus/prometheus.yml");
    }

    /// <summary>
    /// Runs the complete preflight command against the provisioned container.
    /// Required system, service, endpoint, and configuration checks must pass.
    /// Warnings such as an empty test authorized_keys remain non-failing.
    /// </summary>
    [TestMethod]
    public void Preflight_ReportsNoFailedChecks()
    {
        DockerDeployFixture fixture = GetFixture();

        DockerResult result = fixture.Exec("/usr/local/libexec/dotsider-deploy-host", "preflight");

        Assert.AreEqual(0, result.ExitCode, result.StandardError + result.StandardOutput);
        Assert.Contains("0 failed", result.StandardOutput);
        Assert.Contains("Caddy metrics endpoint is reachable", result.StandardOutput);
        Assert.Contains("Prometheus API is reachable", result.StandardOutput);
    }

    /// <summary>
    /// Removes one required directory and verifies that preflight fails clearly.
    /// The directory is restored before the test exits so later tests remain independent.
    /// This retains the prior negative preflight coverage.
    /// </summary>
    [TestMethod]
    public void Preflight_MissingDocsDirectoryFailsAndReportsPath()
    {
        DockerDeployFixture fixture = GetFixture();
        _ = ExecRequired(fixture, "mv", "/var/www/dotsider-docs", "/var/www/dotsider-docs.missing");
        try
        {
            DockerResult result = fixture.Exec("/usr/local/libexec/dotsider-deploy-host", "preflight");

            Assert.AreEqual(1, result.ExitCode);
            Assert.Contains("/var/www/dotsider-docs does not exist", result.StandardOutput);
            Assert.Contains("1 failed", result.StandardOutput);
        }
        finally
        {
            _ = ExecRequired(fixture, "mv", "/var/www/dotsider-docs.missing", "/var/www/dotsider-docs");
        }
    }

    /// <summary>
    /// Queries the real Prometheus endpoint and appends the metrics report log.
    /// The output line retains all five established field names.
    /// A second invocation appends rather than replacing the first line.
    /// </summary>
    [TestMethod]
    public void Report_AppendsRealPrometheusMetrics()
    {
        DockerDeployFixture fixture = GetFixture();
        _ = ExecRequired(fixture, "systemctl", "stop", "caddy-report.timer", "caddy-report.service");
        try
        {
            _ = ExecRequired(fixture, "truncate", "-s", "0", "/var/log/caddy-metrics.log");
            _ = ExecRequired(fixture, "/usr/local/libexec/dotsider-deploy-host", "report");
            _ = ExecRequired(fixture, "/usr/local/libexec/dotsider-deploy-host", "report");
            string log = ExecRequired(fixture, "cat", "/var/log/caddy-metrics.log");

            Assert.HasCount(2, log.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            foreach (string field in new[] { "req/s=", "err/s=", "p95=", "inflight=", "upstream_healthy=" })
            {
                Assert.Contains(field, log);
            }
        }
        finally
        {
            _ = ExecRequired(fixture, "systemctl", "start", "caddy-report.timer");
        }
    }

    /// <summary>
    /// Stops Prometheus and verifies graceful N/A reporting for every query.
    /// The service is restarted in a finally block regardless of assertion outcome.
    /// Reporting remains successful while its data source is unavailable.
    /// </summary>
    [TestMethod]
    public void Report_PrometheusUnavailableWritesNAFields()
    {
        DockerDeployFixture fixture = GetFixture();
        _ = ExecRequired(fixture, "systemctl", "stop", "caddy-report.timer", "caddy-report.service");
        _ = ExecRequired(fixture, "systemctl", "stop", "prometheus");
        try
        {
            _ = ExecRequired(fixture, "truncate", "-s", "0", "/var/log/caddy-metrics.log");
            _ = ExecRequired(fixture, "/usr/local/libexec/dotsider-deploy-host", "report");
            string log = ExecRequired(fixture, "cat", "/var/log/caddy-metrics.log");

            Assert.AreEqual(5, log.Split("N/A", StringSplitOptions.None).Length - 1);
        }
        finally
        {
            _ = ExecRequired(fixture, "systemctl", "start", "prometheus");
            _ = ExecRequired(fixture, "systemctl", "start", "caddy-report.timer");
        }
    }

    /// <summary>
    /// Corrupts the deployed sample and runs the real integrity recovery command.
    /// The complete backup restores the changed file and restarts the website.
    /// Both established corruption and restoration log records must be present.
    /// </summary>
    [TestMethod]
    public void Integrity_RestoresCorruptedSampleAndRestartsWebsite()
    {
        DockerDeployFixture fixture = GetFixture();
        string before = ExecRequired(fixture, "sha256sum", "/opt/dotsider-website/sample/RichLibrary.dll").Split(' ')[0];
        _ = ExecRequired(fixture, "truncate", "-s", "0", "/opt/dotsider-website/sample/RichLibrary.dll");

        _ = ExecRequired(fixture, "/usr/local/libexec/dotsider-deploy-host", "integrity");

        string after = ExecRequired(fixture, "sha256sum", "/opt/dotsider-website/sample/RichLibrary.dll").Split(' ')[0];
        string log = ExecRequired(fixture, "cat", "/var/log/integrity-check.log");
        Assert.AreEqual(before, after);
        Assert.Contains("CORRUPTED sample payload — restoring from backup", log);
        Assert.Contains("RESTORED sample/ and restarted dotsider-website", log);
        Assert.AreEqual("active", ExecRequired(fixture, "systemctl", "is-active", "dotsider-website").Trim());
    }

    /// <summary>
    /// Removes the integrity manifest and verifies the established successful no-op.
    /// The original manifest is restored before the test completes.
    /// No corruption log entry is added when prerequisites are absent.
    /// </summary>
    [TestMethod]
    public void Integrity_MissingManifestIsSuccessfulNoOp()
    {
        DockerDeployFixture fixture = GetFixture();
        _ = ExecRequired(fixture, "mv", "/opt/dotsider-website/sample.sha256", "/opt/dotsider-website/sample.sha256.missing");
        string logBefore = fixture.Exec("cat", "/var/log/integrity-check.log").StandardOutput;
        try
        {
            DockerResult result = fixture.Exec("/usr/local/libexec/dotsider-deploy-host", "integrity");
            string logAfter = fixture.Exec("cat", "/var/log/integrity-check.log").StandardOutput;

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            Assert.AreEqual(logBefore, logAfter);
        }
        finally
        {
            _ = ExecRequired(
                fixture,
                "mv",
                "/opt/dotsider-website/sample.sha256.missing",
                "/opt/dotsider-website/sample.sha256");
        }
    }

    private static DockerDeployFixture GetFixture()
    {
        if (s_fixture is null)
        {
            Assert.Inconclusive("Set DOTSIDER_RUN_DEPLOY_INTEGRATION=1 to run the Debian deployment tests.");
        }

        return s_fixture;
    }

    private static string ExecRequired(DockerDeployFixture fixture, params string[] arguments)
    {
        DockerResult result = fixture.Exec(arguments);
        Assert.AreEqual(0, result.ExitCode, result.StandardError + result.StandardOutput);
        return result.StandardOutput;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dotsider.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
