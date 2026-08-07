namespace Dotsider.DeployHost;

/// <summary>
/// Installs the running Native AOT host at its fixed root-owned location.
/// Candidate bytes are written beside the destination and moved atomically.
/// The installed executable is never writable by the deployment user.
/// </summary>
internal sealed class DeployHostInstaller(IProcessRunner processRunner)
{
    private const UnixFileMode ExecutableMode = UnixFileMode.UserRead
        | UnixFileMode.UserWrite
        | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead
        | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead
        | UnixFileMode.OtherExecute;

    /// <summary>
    /// Copies the current executable into the root-owned libexec directory.
    /// Identical installed bytes are retained while ownership and mode are repaired.
    /// The operation fails before changing services when the process path is unavailable.
    /// </summary>
    /// <param name="cancellationToken">Stops external ownership updates.</param>
    /// <returns>Whether the installed executable bytes changed.</returns>
    internal async Task<bool> InstallAsync(CancellationToken cancellationToken)
    {
        string sourcePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The deploy host process path is unavailable.");
        string destinationPath = DeployPaths.InstalledHostPath;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        if (File.Exists(destinationPath)
            && (File.GetAttributes(destinationPath) & FileAttributes.ReparsePoint) != 0)
        {
            File.Delete(destinationPath);
        }

        bool changed = !File.Exists(destinationPath)
            || !await FilesEqualAsync(sourcePath, destinationPath, cancellationToken).ConfigureAwait(false);
        if (changed && !Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(destinationPath), StringComparison.Ordinal))
        {
            string candidatePath = destinationPath + ".new-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Copy(sourcePath, candidatePath, overwrite: false);
                File.SetUnixFileMode(candidatePath, ExecutableMode);
                await RequireSuccessAsync(
                    "/usr/bin/chown",
                    ["root:root", candidatePath],
                    cancellationToken).ConfigureAwait(false);
                File.Move(candidatePath, destinationPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(candidatePath))
                {
                    File.Delete(candidatePath);
                }
            }
        }

        File.SetUnixFileMode(destinationPath, ExecutableMode);
        await RequireSuccessAsync(
            "/usr/bin/chown",
            ["root:root", destinationPath],
            cancellationToken).ConfigureAwait(false);
        return changed;
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

    private static async Task<bool> FilesEqualAsync(
        string firstPath,
        string secondPath,
        CancellationToken cancellationToken)
    {
        var firstInfo = new FileInfo(firstPath);
        var secondInfo = new FileInfo(secondPath);
        if (firstInfo.Length != secondInfo.Length)
        {
            return false;
        }

        const int bufferSize = 64 * 1024;
        await using FileStream first = File.OpenRead(firstPath);
        await using FileStream second = File.OpenRead(secondPath);
        var firstBuffer = new byte[bufferSize];
        var secondBuffer = new byte[bufferSize];
        while (true)
        {
            int firstRead = await first.ReadAsync(firstBuffer, cancellationToken).ConfigureAwait(false);
            int secondRead = await second.ReadAsync(secondBuffer, cancellationToken).ConfigureAwait(false);
            if (firstRead != secondRead)
            {
                return false;
            }

            if (firstRead == 0)
            {
                return true;
            }

            if (!firstBuffer.AsSpan(0, firstRead).SequenceEqual(secondBuffer.AsSpan(0, secondRead)))
            {
                return false;
            }
        }
    }
}
