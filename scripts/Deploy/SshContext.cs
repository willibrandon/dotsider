/// <summary>
/// Owns the temporary SSH identity and known-hosts files used by deployment.
/// Files receive user-only permissions on Unix before any network command runs.
/// Disposal removes the complete temporary credential directory.
/// </summary>
internal sealed class SshContext : IDisposable
{
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute;
    private const UnixFileMode FileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly string _directory;

    /// <summary>
    /// Creates isolated credential files and records the remote host key.
    /// Host-key discovery retains the deployment workflow's existing ssh-keyscan behavior.
    /// A connection is not attempted unless at least one key is returned.
    /// </summary>
    /// <param name="privateKey">The SSH private key content.</param>
    /// <param name="host">The validated deployment host.</param>
    /// <param name="processRunner">The shell-free local process runner.</param>
    /// <param name="workingDirectory">The process working directory.</param>
    internal SshContext(
        string privateKey,
        string host,
        IDeploymentProcessRunner processRunner,
        string workingDirectory)
    {
        string knownHosts = ScanHostKey(host, processRunner, workingDirectory);
        _directory = Path.Combine(Path.GetTempPath(), "dotsider-deploy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        IdentityPath = Path.Combine(_directory, "identity");
        KnownHostsPath = Path.Combine(_directory, "known_hosts");
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(_directory, DirectoryMode);
            }

            File.WriteAllText(IdentityPath, privateKey.TrimEnd() + "\n", new System.Text.UTF8Encoding(false));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(IdentityPath, FileMode);
            }

            File.WriteAllText(KnownHostsPath, knownHosts.TrimEnd() + "\n", new System.Text.UTF8Encoding(false));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(KnownHostsPath, FileMode);
            }
        }
        catch
        {
            Directory.Delete(_directory, recursive: true);
            throw;
        }
    }

    /// <summary>
    /// Gets the temporary SSH private-key path.
    /// The value is suitable for a literal process argument.
    /// The file is removed during disposal.
    /// </summary>
    internal string IdentityPath { get; }

    /// <summary>
    /// Gets the temporary scanned known-hosts path.
    /// Strict host-key checking uses this file for every connection.
    /// The file is removed during disposal.
    /// </summary>
    internal string KnownHostsPath { get; }

    /// <summary>
    /// Builds the common SSH option arguments used by ssh and scp.
    /// Options disable interactive prompts and enforce the supplied host key.
    /// Each returned value preserves one process argument boundary.
    /// </summary>
    /// <returns>The common SSH arguments.</returns>
    internal string[] CreateArguments()
    {
        return
        [
            "-i",
            IdentityPath,
            "-o",
            $"UserKnownHostsFile={KnownHostsPath}",
            "-o",
            "StrictHostKeyChecking=yes",
            "-o",
            "BatchMode=yes",
        ];
    }

    /// <summary>
    /// Removes all temporary credential material.
    /// Cleanup is attempted after successful and failed deployment operations.
    /// A missing directory is treated as already disposed.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static string ScanHostKey(
        string host,
        IDeploymentProcessRunner processRunner,
        string workingDirectory)
    {
        DeploymentProcessResult result = processRunner.Run(
            "ssh-keyscan",
            ["-H", host],
            workingDirectory,
            64 * 1024,
            TimeSpan.FromSeconds(15));
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new InvalidOperationException(
                $"ssh-keyscan did not return a host key: {result.StandardError.Trim()}");
        }

        return result.StandardOutput;
    }
}
