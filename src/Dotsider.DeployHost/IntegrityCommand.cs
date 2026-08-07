namespace Dotsider.DeployHost;

/// <summary>
/// Verifies the deployed sample payload and restores its preserved backup on drift.
/// Missing prerequisites remain a successful no-op for compatibility with startup ordering.
/// Restoration retains ownership before the website service is restarted.
/// </summary>
internal sealed class IntegrityCommand(
    IProcessRunner processRunner,
    TimeProvider timeProvider,
    string sampleDirectory,
    string backupDirectory,
    string manifestPath,
    string logPath)
{
    /// <summary>
    /// Checks the sample manifest and restores the whole payload when verification fails.
    /// The existing backup is copied with archive semantics to retain ownership and modes.
    /// Corruption and restoration events retain the established UTC log messages.
    /// </summary>
    /// <param name="cancellationToken">Stops hashing, copying, and service control.</param>
    /// <returns>Zero after verification or successful restoration.</returns>
    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sampleDirectory)
            || !Directory.Exists(backupDirectory)
            || !File.Exists(manifestPath))
        {
            return 0;
        }

        if ((File.GetAttributes(sampleDirectory) & FileAttributes.ReparsePoint) != 0
            || (File.GetAttributes(backupDirectory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Sample integrity paths must not be symbolic links.");
        }

        if (await SampleManifest.VerifyAsync(sampleDirectory, manifestPath, cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        string timestamp = timeProvider.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        await File.AppendAllTextAsync(
            logPath,
            $"{timestamp} | CORRUPTED sample payload — restoring from backup{Environment.NewLine}",
            cancellationToken).ConfigureAwait(false);
        Directory.Delete(sampleDirectory, recursive: true);
        await RequireSuccessAsync(
            "/usr/bin/cp",
            ["-a", "--", backupDirectory, sampleDirectory],
            cancellationToken).ConfigureAwait(false);
        _ = await processRunner.RunAsync(
            "/usr/bin/systemctl",
            ["reset-failed", DeployPaths.WebsiteService],
            cancellationToken).ConfigureAwait(false);
        await RequireSuccessAsync(
            "/usr/bin/systemctl",
            ["restart", DeployPaths.WebsiteService],
            cancellationToken).ConfigureAwait(false);
        await File.AppendAllTextAsync(
            logPath,
            $"{timestamp} | RESTORED sample/ and restarted dotsider-website{Environment.NewLine}",
            cancellationToken).ConfigureAwait(false);
        return 0;
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
