using System.Runtime.InteropServices;

namespace Dotsider.DeployHost;

/// <summary>
/// Verifies that the production host can receive the established dotsider deployment.
/// Checks cover system tools, services, ownership, firewall, resources, and configuration.
/// All checks run so one invocation reports every actionable problem.
/// </summary>
internal sealed class PreflightCommand(
    IProcessRunner processRunner,
    HttpClient httpClient,
    TextWriter writer)
{
    /// <summary>
    /// Runs every preflight check and prints a complete result summary.
    /// Warnings remain non-blocking while failed requirements return exit code one.
    /// The command does not change host configuration or service state.
    /// </summary>
    /// <param name="cancellationToken">Stops process and HTTP checks.</param>
    /// <returns>Zero when no required check fails; otherwise one.</returns>
    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var reporter = new PreflightReporter(writer);
        CheckSystem(reporter);
        await CheckDeployHostAsync(reporter, cancellationToken).ConfigureAwait(false);
        await CheckCaddyAsync(reporter, cancellationToken).ConfigureAwait(false);
        CheckTools(reporter);
        await CheckUserAsync(reporter, cancellationToken).ConfigureAwait(false);
        await CheckDirectoriesAsync(reporter, cancellationToken).ConfigureAwait(false);
        await CheckWebsiteServiceAsync(reporter, cancellationToken).ConfigureAwait(false);
        await CheckFirewallAsync(reporter, cancellationToken).ConfigureAwait(false);
        await CheckPrometheusAsync(reporter, cancellationToken).ConfigureAwait(false);
        await CheckTimersAsync(reporter, cancellationToken).ConfigureAwait(false);
        CheckResources(reporter);
        CheckCaddyConfiguration(reporter);
        reporter.Summary();
        return reporter.Failed == 0 ? 0 : 1;
    }

    private static void CheckSystem(PreflightReporter reporter)
    {
        reporter.Section("System");
        if (OperatingSystem.IsLinux())
        {
            reporter.Pass($"Linux detected ({Environment.OSVersion.VersionString})");
        }
        else
        {
            reporter.Fail($"Expected Linux, got {RuntimeInformation.OSDescription}");
        }

        CheckDistribution(reporter);
        if (RuntimeInformation.OSArchitecture == Architecture.X64)
        {
            reporter.Pass("Architecture: x86_64");
        }
        else if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            reporter.Pass("Architecture: aarch64");
        }
        else
        {
            reporter.Fail($"Unsupported architecture: {RuntimeInformation.OSArchitecture}");
        }
    }

    private static void CheckDistribution(PreflightReporter reporter)
    {
        const string path = "/etc/os-release";
        if (!File.Exists(path))
        {
            reporter.Fail("Linux distribution information is unavailable");
            return;
        }

        Dictionary<string, string> values = File.ReadLines(path)
            .Select(static line => line.Split('=', 2))
            .Where(static parts => parts.Length == 2)
            .ToDictionary(
                static parts => parts[0],
                static parts => parts[1].Trim('"'),
                StringComparer.Ordinal);
        values.TryGetValue("ID", out string? id);
        values.TryGetValue("VERSION_ID", out string? version);
        if (id == "debian" && version == "13")
        {
            reporter.Pass("Distribution: Debian 13");
        }
        else
        {
            reporter.Fail($"Unsupported distribution: {id ?? "unknown"} {version ?? "unknown"}");
        }
    }

    private async Task CheckDeployHostAsync(
        PreflightReporter reporter,
        CancellationToken cancellationToken)
    {
        reporter.Section("Deploy host");
        if (!File.Exists(DeployPaths.InstalledHostPath))
        {
            reporter.Warn("Native AOT deploy host is not installed yet");
            return;
        }

        UnixFileMode mode = File.GetUnixFileMode(DeployPaths.InstalledHostPath);
        UnixFileMode unsafeWrite = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
        if ((mode & unsafeWrite) == 0 && (mode & UnixFileMode.UserExecute) != 0)
        {
            reporter.Pass("Native AOT deploy host is executable and not group/world writable");
        }
        else
        {
            reporter.Fail("Native AOT deploy host permissions are unsafe");
        }

        ProcessResult owner = await processRunner.RunAsync(
            "/usr/bin/stat",
            ["-c", "%U:%G", DeployPaths.InstalledHostPath],
            cancellationToken).ConfigureAwait(false);
        if (owner.ExitCode == 0 && owner.StandardOutput.Trim().Equals("root:root", StringComparison.Ordinal))
        {
            reporter.Pass("Native AOT deploy host is owned by root:root");
        }
        else
        {
            reporter.Fail("Native AOT deploy host must be owned by root:root");
        }
    }

    private async Task CheckCaddyAsync(PreflightReporter reporter, CancellationToken cancellationToken)
    {
        reporter.Section("Caddy");
        if (!File.Exists("/usr/bin/caddy"))
        {
            reporter.Fail("Caddy not found");
            return;
        }

        ProcessResult version = await processRunner.RunAsync(
            "/usr/bin/caddy",
            ["version"],
            cancellationToken).ConfigureAwait(false);
        reporter.Pass($"Caddy installed ({version.StandardOutput.Trim()})");
        await ReportServiceStateAsync(reporter, "caddy", "Caddy", cancellationToken).ConfigureAwait(false);
    }

    private static void CheckTools(PreflightReporter reporter)
    {
        reporter.Section("Tools");
        CheckExecutable(reporter, "/usr/bin/rsync", "rsync");
        CheckExecutable(reporter, "/usr/bin/systemctl", "systemd");
        CheckExecutable(reporter, "/usr/bin/journalctl", "journalctl", required: false);
    }

    private async Task CheckUserAsync(PreflightReporter reporter, CancellationToken cancellationToken)
    {
        reporter.Section("User");
        ProcessResult id = await processRunner.RunAsync(
            "/usr/bin/id",
            [DeployPaths.DeployUser],
            cancellationToken).ConfigureAwait(false);
        if (id.ExitCode == 0)
        {
            reporter.Pass($"User '{DeployPaths.DeployUser}' exists");
        }
        else
        {
            reporter.Fail($"User '{DeployPaths.DeployUser}' does not exist");
        }

        string authorizedKeys = $"/home/{DeployPaths.DeployUser}/.ssh/authorized_keys";
        if (File.Exists(authorizedKeys) && new FileInfo(authorizedKeys).Length > 0)
        {
            int count = File.ReadLines(authorizedKeys).Count();
            reporter.Pass($"SSH authorized_keys has {count} key(s)");
        }
        else
        {
            reporter.Warn($"No SSH authorized_keys for '{DeployPaths.DeployUser}'");
        }

        ProcessResult sudo = await processRunner.RunAsync(
            "/usr/bin/sudo",
            ["-n", "true"],
            cancellationToken).ConfigureAwait(false);
        if (sudo.ExitCode == 0)
        {
            reporter.Pass($"User '{DeployPaths.DeployUser}' has sudo access");
        }
        else
        {
            reporter.Warn($"User '{DeployPaths.DeployUser}' does not have passwordless sudo");
        }
    }

    private async Task CheckDirectoriesAsync(
        PreflightReporter reporter,
        CancellationToken cancellationToken)
    {
        reporter.Section("Directories");
        foreach (string path in new[] { DeployPaths.DocsDirectory, DeployPaths.WebsiteDirectory })
        {
            if (!Directory.Exists(path))
            {
                reporter.Fail($"{path} does not exist");
                continue;
            }

            ProcessResult owner = await processRunner.RunAsync(
                "/usr/bin/stat",
                ["-c", "%U", path],
                cancellationToken).ConfigureAwait(false);
            if (owner.ExitCode == 0
                && owner.StandardOutput.Trim().Equals(DeployPaths.DeployUser, StringComparison.Ordinal))
            {
                reporter.Pass($"{path} exists (owned by {DeployPaths.DeployUser})");
            }
            else
            {
                reporter.Warn($"{path} exists but is not owned by {DeployPaths.DeployUser}");
            }
        }
    }

    private async Task CheckWebsiteServiceAsync(
        PreflightReporter reporter,
        CancellationToken cancellationToken)
    {
        reporter.Section("systemd Service");
        ProcessResult enabled = await processRunner.RunAsync(
            "/usr/bin/systemctl",
            ["is-enabled", DeployPaths.WebsiteService],
            cancellationToken).ConfigureAwait(false);
        if (enabled.ExitCode == 0)
        {
            reporter.Pass("dotsider-website.service is enabled");
        }
        else if (File.Exists("/etc/systemd/system/dotsider-website.service"))
        {
            reporter.Warn("dotsider-website.service exists but is not enabled");
        }
        else
        {
            reporter.Warn("dotsider-website.service not installed yet");
        }
    }

    private async Task CheckFirewallAsync(PreflightReporter reporter, CancellationToken cancellationToken)
    {
        reporter.Section("Firewall");
        if (!File.Exists("/usr/sbin/ufw"))
        {
            reporter.Warn("No UFW firewall detected");
            return;
        }

        ProcessResult status = await processRunner.RunAsync(
            "/usr/bin/sudo",
            ["-n", "/usr/sbin/ufw", "status"],
            cancellationToken).ConfigureAwait(false);
        if (status.StandardOutput.Contains("inactive", StringComparison.OrdinalIgnoreCase))
        {
            reporter.Warn("ufw is inactive");
            return;
        }

        foreach (int port in new[] { 80, 443 })
        {
            if (status.StandardOutput.Contains(port.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                reporter.Pass($"Port {port} allowed in ufw");
            }
            else
            {
                reporter.Fail($"Port {port} not in ufw");
            }
        }

        if (status.StandardOutput.Contains("22", StringComparison.Ordinal))
        {
            reporter.Pass("Port 22 (SSH) allowed in ufw");
        }
        else
        {
            reporter.Warn("Port 22 not explicitly in ufw");
        }
    }

    private async Task CheckPrometheusAsync(
        PreflightReporter reporter,
        CancellationToken cancellationToken)
    {
        reporter.Section("Prometheus");
        if (File.Exists("/usr/bin/prometheus"))
        {
            ProcessResult version = await processRunner.RunAsync(
                "/usr/bin/prometheus",
                ["--version"],
                cancellationToken).ConfigureAwait(false);
            string firstLine = version.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                ?? "unknown";
            reporter.Pass($"Prometheus installed ({firstLine.Trim()})");
        }
        else
        {
            reporter.Fail("Prometheus not found");
        }

        await ReportServiceStateAsync(reporter, "prometheus", "Prometheus", cancellationToken).ConfigureAwait(false);
        await CheckEndpointAsync(
            reporter,
            "http://localhost:9090/-/healthy",
            "Prometheus API is reachable",
            "Prometheus API not reachable",
            cancellationToken).ConfigureAwait(false);
        await CheckEndpointAsync(
            reporter,
            "http://localhost:2019/metrics",
            "Caddy metrics endpoint is reachable",
            "Caddy metrics endpoint not reachable",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task CheckTimersAsync(PreflightReporter reporter, CancellationToken cancellationToken)
    {
        reporter.Section("Metrics and integrity timers");
        await ReportTimerStateAsync(reporter, DeployPaths.ReportTimer, cancellationToken).ConfigureAwait(false);
        await ReportTimerStateAsync(reporter, DeployPaths.IntegrityTimer, cancellationToken).ConfigureAwait(false);
    }

    private static void CheckResources(PreflightReporter reporter)
    {
        reporter.Section("Resources");
        var drive = new DriveInfo(Path.GetPathRoot(DeployPaths.WebsiteDirectory)!);
        long availableGibibytes = drive.AvailableFreeSpace / (1024L * 1024L * 1024L);
        if (availableGibibytes >= 2)
        {
            reporter.Pass($"Disk: {availableGibibytes}G available");
        }
        else
        {
            reporter.Warn($"Disk: only {availableGibibytes}G available (recommend 2G+)");
        }

        long memoryMibibytes = ReadMemoryMibibytes();
        if (memoryMibibytes >= 512)
        {
            reporter.Pass($"Memory: {memoryMibibytes}MB total");
        }
        else if (memoryMibibytes > 0)
        {
            reporter.Warn($"Memory: only {memoryMibibytes}MB (recommend 512MB+)");
        }
    }

    private static void CheckCaddyConfiguration(PreflightReporter reporter)
    {
        reporter.Section("Caddy Config");
        const string path = "/etc/caddy/Caddyfile";
        if (!File.Exists(path))
        {
            reporter.Warn("Caddyfile not found");
            return;
        }

        string text = File.ReadAllText(path);
        if (text.Contains("dotsider", StringComparison.Ordinal))
        {
            reporter.Pass("Caddyfile references dotsider");
        }
        else
        {
            reporter.Warn("Caddyfile exists but does not mention dotsider");
        }

        if (text.Contains("metrics", StringComparison.Ordinal))
        {
            reporter.Pass("Caddyfile has metrics enabled");
        }
        else
        {
            reporter.Warn("Caddyfile does not enable metrics");
        }
    }

    private static void CheckExecutable(
        PreflightReporter reporter,
        string path,
        string name,
        bool required = true)
    {
        if (File.Exists(path))
        {
            reporter.Pass($"{name} installed");
        }
        else if (required)
        {
            reporter.Fail($"{name} not found");
        }
        else
        {
            reporter.Warn($"{name} not found");
        }
    }

    private async Task ReportServiceStateAsync(
        PreflightReporter reporter,
        string service,
        string displayName,
        CancellationToken cancellationToken)
    {
        ProcessResult active = await processRunner.RunAsync(
            "/usr/bin/systemctl",
            ["is-active", "--quiet", service],
            cancellationToken).ConfigureAwait(false);
        if (active.ExitCode == 0)
        {
            reporter.Pass($"{displayName} service is running");
            return;
        }

        ProcessResult enabled = await processRunner.RunAsync(
            "/usr/bin/systemctl",
            ["is-enabled", "--quiet", service],
            cancellationToken).ConfigureAwait(false);
        if (enabled.ExitCode == 0)
        {
            reporter.Warn($"{displayName} service is enabled but not running");
        }
        else
        {
            reporter.Warn($"{displayName} service is not enabled");
        }
    }

    private async Task ReportTimerStateAsync(
        PreflightReporter reporter,
        string timer,
        CancellationToken cancellationToken)
    {
        ProcessResult active = await processRunner.RunAsync(
            "/usr/bin/systemctl",
            ["is-active", "--quiet", timer],
            cancellationToken).ConfigureAwait(false);
        if (active.ExitCode == 0)
        {
            reporter.Pass($"{timer} is active");
            return;
        }

        ProcessResult enabled = await processRunner.RunAsync(
            "/usr/bin/systemctl",
            ["is-enabled", "--quiet", timer],
            cancellationToken).ConfigureAwait(false);
        if (enabled.ExitCode == 0)
        {
            reporter.Warn($"{timer} is enabled but not active");
        }
        else
        {
            reporter.Warn($"{timer} is not installed yet");
        }
    }

    private async Task CheckEndpointAsync(
        PreflightReporter reporter,
        string url,
        string successMessage,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(3));
            using HttpResponseMessage response = await httpClient.GetAsync(url, timeoutSource.Token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                reporter.Pass(successMessage);
            }
            else
            {
                reporter.Warn(failureMessage);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            reporter.Warn(failureMessage);
        }
    }

    private static long ReadMemoryMibibytes()
    {
        const string path = "/proc/meminfo";
        if (!File.Exists(path))
        {
            return 0;
        }

        string? line = File.ReadLines(path).FirstOrDefault(
            static value => value.StartsWith("MemTotal:", StringComparison.Ordinal));
        if (line is null)
        {
            return 0;
        }

        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            && long.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out long kibibytes)
                ? kibibytes / 1024
                : 0;
    }
}
