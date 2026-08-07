using System.Reflection;

namespace Dotsider.DeployHost;

/// <summary>
/// Validates and installs authoritative configuration embedded in the host binary.
/// Every candidate is checked before any destination is replaced atomically.
/// Change tracking lets callers reload only services whose inputs changed.
/// </summary>
internal sealed class EmbeddedAssetInstaller(IProcessRunner processRunner, Assembly? assembly = null)
{
    private readonly Assembly _assembly = assembly ?? typeof(EmbeddedAssetInstaller).Assembly;

    /// <summary>
    /// Extracts, validates, and installs all manifest entries.
    /// Candidate validation completes before privileged destinations are changed.
    /// Installed files receive the exact manifest ownership and mode.
    /// </summary>
    /// <param name="cancellationToken">Stops validation and installation.</param>
    /// <returns>The groups whose installed bytes changed.</returns>
    internal async Task<InstallChanges> InstallAsync(CancellationToken cancellationToken)
    {
        InstallManifest manifest = InstallManifestLoader.Load(_assembly);
        string stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            "dotsider-deploy-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        File.SetUnixFileMode(
            stagingDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        try
        {
            var candidates = new Dictionary<InstallFile, string>();
            foreach (InstallFile file in manifest.Files)
            {
                string candidatePath = Path.Combine(stagingDirectory, Path.GetFileName(file.Destination));
                await ExtractAsync(file.Resource, candidatePath, cancellationToken).ConfigureAwait(false);
                candidates.Add(file, candidatePath);
            }

            await ValidateCandidatesAsync(candidates, cancellationToken).ConfigureAwait(false);

            var caddyChanged = false;
            var prometheusChanged = false;
            var systemdChanged = false;
            foreach ((InstallFile file, string candidatePath) in candidates)
            {
                bool changed = await InstallFileAsync(file, candidatePath, cancellationToken).ConfigureAwait(false);
                caddyChanged |= changed && file.Destination.Equals("/etc/caddy/Caddyfile", StringComparison.Ordinal);
                prometheusChanged |= changed && file.Destination.Equals("/etc/prometheus/prometheus.yml", StringComparison.Ordinal);
                systemdChanged |= changed && file.Destination.StartsWith("/etc/systemd/system/", StringComparison.Ordinal);
            }

            return new InstallChanges(caddyChanged, prometheusChanged, systemdChanged);
        }
        finally
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    private async Task ExtractAsync(
        string resourceName,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using Stream source = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' is missing.");
        await using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task ValidateCandidatesAsync(
        IReadOnlyDictionary<InstallFile, string> candidates,
        CancellationToken cancellationToken)
    {
        string caddyPath = FindCandidate(candidates, "/etc/caddy/Caddyfile");
        await RequireSuccessAsync(
            "/usr/bin/caddy",
            ["validate", "--config", caddyPath],
            cancellationToken).ConfigureAwait(false);

        string prometheusPath = FindCandidate(candidates, "/etc/prometheus/prometheus.yml");
        await RequireSuccessAsync(
            "/usr/bin/promtool",
            ["check", "config", prometheusPath],
            cancellationToken).ConfigureAwait(false);

        string[] unitPaths = [.. candidates
            .Where(static pair => pair.Key.Destination.StartsWith("/etc/systemd/system/", StringComparison.Ordinal))
            .Where(static pair => File.Exists(DeployPaths.WebsiteExecutablePath)
                || !pair.Key.Destination.Equals(
                    "/etc/systemd/system/dotsider-website.service",
                    StringComparison.Ordinal))
            .Select(static pair => pair.Value)];
        await RequireSuccessAsync(
            "/usr/bin/systemd-analyze",
            ["verify", .. unitPaths],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> InstallFileAsync(
        InstallFile file,
        string candidatePath,
        CancellationToken cancellationToken)
    {
        byte[] candidateBytes = await File.ReadAllBytesAsync(candidatePath, cancellationToken).ConfigureAwait(false);
        if (File.Exists(file.Destination)
            && (File.GetAttributes(file.Destination) & FileAttributes.ReparsePoint) == 0)
        {
            byte[] installedBytes = await File.ReadAllBytesAsync(file.Destination, cancellationToken).ConfigureAwait(false);
            if (candidateBytes.AsSpan().SequenceEqual(installedBytes))
            {
                File.SetUnixFileMode(file.Destination, ParseMode(file.Mode));
                await RequireSuccessAsync(
                    "/usr/bin/chown",
                    [$"{file.Owner}:{file.Group}", file.Destination],
                    cancellationToken).ConfigureAwait(false);
                return false;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(file.Destination)!);
        string temporaryPath = file.Destination + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, candidateBytes, cancellationToken).ConfigureAwait(false);
            File.SetUnixFileMode(temporaryPath, ParseMode(file.Mode));
            await RequireSuccessAsync(
                "/usr/bin/chown",
                [$"{file.Owner}:{file.Group}", temporaryPath],
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, file.Destination, overwrite: true);
            return true;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
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

    private static string FindCandidate(
        IReadOnlyDictionary<InstallFile, string> candidates,
        string destination)
    {
        return candidates.Single(pair => pair.Key.Destination.Equals(destination, StringComparison.Ordinal)).Value;
    }

    private static UnixFileMode ParseMode(string mode)
    {
        int value = Convert.ToInt32(mode, 8);
        return (UnixFileMode)value;
    }
}
