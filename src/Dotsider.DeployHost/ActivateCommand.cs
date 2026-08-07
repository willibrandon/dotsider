namespace Dotsider.DeployHost;

/// <summary>
/// Activates an uploaded website, documentation site, sample, and deployment host.
/// The command refreshes validated configuration and the sample recovery artifacts.
/// Established services and timers retain their names and filesystem layout.
/// </summary>
internal sealed class ActivateCommand(
    IProcessRunner processRunner,
    HttpClient httpClient,
    TextWriter writer)
{
    /// <summary>
    /// Installs the candidate host, applies embedded assets, and activates deployed content.
    /// Service configuration is validated before it replaces installed files.
    /// The website health endpoint must respond successfully before completion.
    /// </summary>
    /// <param name="cancellationToken">Stops installation and health verification.</param>
    /// <returns>Zero after the deployed website is healthy.</returns>
    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        writer.WriteLine("Installing deployment host and configuration");
        _ = await new DeployHostInstaller(processRunner).InstallAsync(cancellationToken).ConfigureAwait(false);
        InstallChanges changes = await new EmbeddedAssetInstaller(processRunner)
            .InstallAsync(cancellationToken).ConfigureAwait(false);
        if (changes.SystemdChanged)
        {
            await RequireSuccessAsync("/usr/bin/systemctl", ["daemon-reload"], cancellationToken).ConfigureAwait(false);
        }

        if (changes.CaddyChanged)
        {
            await RequireSuccessAsync("/usr/bin/systemctl", ["reload", "caddy"], cancellationToken).ConfigureAwait(false);
        }

        if (changes.PrometheusChanged)
        {
            await RequireSuccessAsync("/usr/bin/systemctl", ["restart", "prometheus"], cancellationToken).ConfigureAwait(false);
        }

        writer.WriteLine("Refreshing sample recovery data");
        if (!Directory.Exists(DeployPaths.SampleDirectory))
        {
            throw new DirectoryNotFoundException($"Deployed sample directory '{DeployPaths.SampleDirectory}' was not found.");
        }

        if ((File.GetAttributes(DeployPaths.SampleDirectory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The deployed sample directory must not be a symbolic link.");
        }

        if (Directory.Exists(DeployPaths.SampleBackupDirectory))
        {
            if ((File.GetAttributes(DeployPaths.SampleBackupDirectory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("The sample backup directory must not be a symbolic link.");
            }

            Directory.Delete(DeployPaths.SampleBackupDirectory, recursive: true);
        }

        await RequireSuccessAsync(
            "/usr/bin/cp",
            ["-a", "--", DeployPaths.SampleDirectory, DeployPaths.SampleBackupDirectory],
            cancellationToken).ConfigureAwait(false);
        await SampleManifest.CreateAsync(
            DeployPaths.SampleDirectory,
            DeployPaths.SampleManifestPath,
            cancellationToken).ConfigureAwait(false);
        DeleteLegacySampleFiles();

        await RequireSuccessAsync("/usr/bin/systemctl", ["enable", DeployPaths.WebsiteService], cancellationToken).ConfigureAwait(false);
        await RequireSuccessAsync(
            "/usr/bin/systemctl",
            ["enable", "--now", DeployPaths.ReportTimer, DeployPaths.IntegrityTimer],
            cancellationToken).ConfigureAwait(false);
        await RequireSuccessAsync("/usr/bin/systemctl", ["restart", DeployPaths.WebsiteService], cancellationToken).ConfigureAwait(false);
        await VerifyHealthAsync(cancellationToken).ConfigureAwait(false);
        DeleteLegacyScripts();
        writer.WriteLine("Deployment complete");
        return 0;
    }

    private async Task VerifyHealthAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(10));
        using HttpResponseMessage response = await httpClient.GetAsync(
            DeployPaths.WebsiteHealthUrl,
            timeoutSource.Token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Website health check returned HTTP {(int)response.StatusCode}.");
        }
    }

    private static void DeleteLegacySampleFiles()
    {
        foreach (string fileName in new[] { ".RichLibrary.dll.bak", ".RichLibrary.dll.sha256" })
        {
            string path = Path.Combine(DeployPaths.WebsiteDirectory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void DeleteLegacyScripts()
    {
        foreach (string fileName in new[] { "caddy-report.sh", "integrity-check.sh" })
        {
            string path = Path.Combine(DeployPaths.WebsiteDirectory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private async Task RequireSuccessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessResult result = await processRunner.RunAsync(fileName, arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{fileName}' failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
        }
    }
}
