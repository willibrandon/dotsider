namespace Dotsider.Core.Analysis;

/// <summary>
/// Validates executable targets accepted by runtime tracing.
/// Rejects missing files and unsupported Windows launch formats.
/// Requires executable permissions for direct launches on Unix.
/// </summary>
internal static class TraceTargetPath
{
    internal static string Validate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The runtime trace target was not found.", fullPath);

        if (fullPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return fullPath;

        if (OperatingSystem.IsWindows())
        {
            if (!fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Runtime tracing on Windows supports managed DLLs and executable files only.",
                    nameof(path));
            }

            return fullPath;
        }

        const UnixFileMode executePermissions =
            UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        if ((File.GetUnixFileMode(fullPath) & executePermissions) == 0)
        {
            throw new ArgumentException(
                "A runtime trace target launched directly on Unix must be executable.",
                nameof(path));
        }

        return fullPath;
    }
}
