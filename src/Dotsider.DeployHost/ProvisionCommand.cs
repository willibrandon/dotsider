namespace Dotsider.DeployHost;

/// <summary>
/// Provisions the packages, account, directories, configuration, and services used by dotsider.dev.
/// The command retains the established Debian host layout and firewall rules.
/// Package downloads and privileged process calls do not use a command shell.
/// </summary>
internal sealed class ProvisionCommand(
    IProcessRunner processRunner,
    HttpClient httpClient,
    TextWriter writer)
{
    private const UnixFileMode OwnerDirectoryMode = UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute;
    private const UnixFileMode AuthorizedKeysMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode SudoersMode = UnixFileMode.UserRead | UnixFileMode.GroupRead;

    /// <summary>
    /// Installs host prerequisites and the embedded deployment configuration.
    /// Existing accounts, keys, and deployment content are preserved.
    /// Services and timers are enabled using their established names.
    /// </summary>
    /// <param name="cancellationToken">Stops downloads and external commands.</param>
    /// <returns>Zero after provisioning completes.</returns>
    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        writer.WriteLine("Installing packages");
        await AptAsync(["update"], cancellationToken).ConfigureAwait(false);
        await AptAsync(
            [
                "install",
                "-y",
                "ca-certificates",
                "rsync",
                "ufw",
                "sudo",
                "gnupg",
                "debian-keyring",
                "debian-archive-keyring",
                "apt-transport-https",
                "libgssapi-krb5-2",
                "libicu76",
                "libssl3t64",
                "zlib1g",
            ],
            cancellationToken).ConfigureAwait(false);
        await InstallCaddyRepositoryAsync(cancellationToken).ConfigureAwait(false);
        await AptAsync(["update"], cancellationToken).ConfigureAwait(false);
        await AptAsync(["install", "-y", "caddy", "prometheus"], cancellationToken).ConfigureAwait(false);

        writer.WriteLine("Configuring deployment account");
        ProcessResult userResult = await processRunner.RunAsync(
            "/usr/bin/id",
            [DeployPaths.DeployUser],
            cancellationToken).ConfigureAwait(false);
        if (userResult.ExitCode != 0)
        {
            await RequireSuccessAsync(
                "/usr/sbin/adduser",
                ["--disabled-password", "--gecos", string.Empty, DeployPaths.DeployUser],
                cancellationToken).ConfigureAwait(false);
        }

        string sshDirectory = $"/home/{DeployPaths.DeployUser}/.ssh";
        string authorizedKeys = Path.Combine(sshDirectory, "authorized_keys");
        EnsureDirectory(sshDirectory);
        File.SetUnixFileMode(sshDirectory, OwnerDirectoryMode);
        if (File.Exists(authorizedKeys))
        {
            if ((File.GetAttributes(authorizedKeys) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"Refusing symbolic link '{authorizedKeys}'.");
            }
        }
        else
        {
            if (Directory.Exists(authorizedKeys))
            {
                throw new InvalidOperationException($"Expected a file at '{authorizedKeys}'.");
            }

            await File.WriteAllTextAsync(authorizedKeys, string.Empty, cancellationToken).ConfigureAwait(false);
        }

        File.SetUnixFileMode(authorizedKeys, AuthorizedKeysMode);
        await RequireSuccessAsync(
            "/usr/bin/chown",
            ["-R", $"{DeployPaths.DeployUser}:{DeployPaths.DeployUser}", sshDirectory],
            cancellationToken).ConfigureAwait(false);
        await InstallSudoersAsync(cancellationToken).ConfigureAwait(false);

        writer.WriteLine("Installing deployment services");
        EnsureDirectory(DeployPaths.DocsDirectory);
        EnsureDirectory(DeployPaths.WebsiteDirectory);
        await RequireSuccessAsync(
            "/usr/bin/chown",
            [$"{DeployPaths.DeployUser}:{DeployPaths.DeployUser}", DeployPaths.DocsDirectory, DeployPaths.WebsiteDirectory],
            cancellationToken).ConfigureAwait(false);

        _ = await new DeployHostInstaller(processRunner).InstallAsync(cancellationToken).ConfigureAwait(false);
        _ = await new EmbeddedAssetInstaller(processRunner).InstallAsync(cancellationToken).ConfigureAwait(false);
        await RequireSuccessAsync("/usr/bin/systemctl", ["daemon-reload"], cancellationToken).ConfigureAwait(false);
        await RequireSuccessAsync("/usr/bin/systemctl", ["enable", DeployPaths.WebsiteService], cancellationToken).ConfigureAwait(false);
        await RequireSuccessAsync("/usr/bin/systemctl", ["enable", "caddy", "prometheus"], cancellationToken).ConfigureAwait(false);
        await RequireSuccessAsync(
            "/usr/bin/systemctl",
            ["enable", "--now", DeployPaths.ReportTimer, DeployPaths.IntegrityTimer],
            cancellationToken).ConfigureAwait(false);
        await RequireSuccessAsync("/usr/bin/systemctl", ["restart", "caddy", "prometheus"], cancellationToken).ConfigureAwait(false);

        writer.WriteLine("Configuring firewall");
        foreach (string rule in new[] { "22/tcp", "80/tcp", "443/tcp" })
        {
            await RequireSuccessAsync("/usr/sbin/ufw", ["allow", rule], cancellationToken).ConfigureAwait(false);
        }

        await RequireSuccessAsync("/usr/sbin/ufw", ["--force", "enable"], cancellationToken).ConfigureAwait(false);
        writer.WriteLine("Setup complete");
        return 0;
    }

    private async Task InstallCaddyRepositoryAsync(CancellationToken cancellationToken)
    {
        const string keyUrl = "https://dl.cloudsmith.io/public/caddy/stable/gpg.key";
        const string listUrl = "https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt";
        const string keyDestination = "/usr/share/keyrings/caddy-stable-archive-keyring.gpg";
        const string listDestination = "/etc/apt/sources.list.d/caddy-stable.list";
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "dotsider-caddy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        File.SetUnixFileMode(temporaryDirectory, OwnerDirectoryMode);
        try
        {
            string keySource = Path.Combine(temporaryDirectory, "caddy.gpg.key");
            string keyCandidate = Path.Combine(temporaryDirectory, "caddy.gpg");
            string listCandidate = Path.Combine(temporaryDirectory, "caddy.list");
            await DownloadAsync(keyUrl, keySource, cancellationToken).ConfigureAwait(false);
            await DownloadAsync(listUrl, listCandidate, cancellationToken).ConfigureAwait(false);
            await RequireSuccessAsync(
                "/usr/bin/gpg",
                ["--batch", "--yes", "--dearmor", "--output", keyCandidate, keySource],
                cancellationToken).ConfigureAwait(false);
            File.Copy(keyCandidate, keyDestination, overwrite: true);
            File.Copy(listCandidate, listDestination, overwrite: true);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private async Task DownloadAsync(string requestUri, string destinationPath, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using FileStream destination = new(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    private async Task InstallSudoersAsync(CancellationToken cancellationToken)
    {
        const string destination = "/etc/sudoers.d/brandon";
        string candidate = destination + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(
                candidate,
                "brandon ALL=(ALL) NOPASSWD: ALL\n",
                new System.Text.UTF8Encoding(false),
                cancellationToken).ConfigureAwait(false);
            File.SetUnixFileMode(candidate, SudoersMode);
            await RequireSuccessAsync("/usr/sbin/visudo", ["-c", "-f", candidate], cancellationToken).ConfigureAwait(false);
            File.Move(candidate, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }

    private Task AptAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return RequireSuccessAsync("/usr/bin/apt-get", arguments, cancellationToken);
    }

    private static void EnsureDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException($"Refusing symbolic link '{path}'.");
            }

            return;
        }

        if (File.Exists(path))
        {
            throw new InvalidOperationException($"Expected a directory at '{path}'.");
        }

        Directory.CreateDirectory(path);
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
