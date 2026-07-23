namespace Dotsider.Views;

/// <summary>
/// Resolves configured editor executables without searching an untrusted current directory.
/// </summary>
internal static class EditorExecutableResolver
{
    private const string DefaultPathExtensions = ".COM;.EXE;.BAT;.CMD";

    /// <summary>
    /// Resolves an editor command using the current platform and process environment.
    /// </summary>
    /// <param name="command">The parsed editor command.</param>
    /// <param name="resolvedPath">The absolute executable path when found.</param>
    /// <returns><see langword="true"/> when an eligible executable was found.</returns>
    internal static bool TryResolve(EditorCommand command, out string resolvedPath)
    {
        var pathEntries = SplitPathEntries(Environment.GetEnvironmentVariable("PATH"));
        return OperatingSystem.IsWindows()
            ? TryResolveWindows(
                command.Executable,
                pathEntries,
                SplitPathExtensions(Environment.GetEnvironmentVariable("PATHEXT")),
                out resolvedPath)
            : TryResolveUnix(command.Executable, pathEntries, out resolvedPath);
    }

    /// <summary>
    /// Resolves a Windows editor through explicit paths or rooted PATH entries and PATHEXT.
    /// </summary>
    /// <param name="token">The configured executable token.</param>
    /// <param name="pathEntries">The PATH entries to search.</param>
    /// <param name="pathExtensions">The PATHEXT entries to apply.</param>
    /// <param name="resolvedPath">The absolute executable path when found.</param>
    /// <returns><see langword="true"/> when an eligible executable was found.</returns>
    internal static bool TryResolveWindows(
        string token,
        IReadOnlyList<string> pathEntries,
        IReadOnlyList<string> pathExtensions,
        out string resolvedPath)
    {
        resolvedPath = "";
        if (ContainsWindowsDirectorySeparator(token))
        {
            string explicitPath;
            try
            {
                explicitPath = Path.GetFullPath(token);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                return false;
            }

            return TryWindowsCandidate(explicitPath, pathExtensions, out resolvedPath);
        }

        foreach (var entry in pathEntries)
        {
            if (!TryNormalizeRootedPathEntry(entry, out var directory))
                continue;

            if (TryWindowsCandidate(Path.Combine(directory, token), pathExtensions, out resolvedPath))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves a Unix editor through an explicit path or rooted PATH entries.
    /// </summary>
    /// <param name="token">The configured executable token.</param>
    /// <param name="pathEntries">The PATH entries to search.</param>
    /// <param name="resolvedPath">The absolute executable path when found.</param>
    /// <returns><see langword="true"/> when an executable regular file was found.</returns>
    internal static bool TryResolveUnix(
        string token,
        IReadOnlyList<string> pathEntries,
        out string resolvedPath)
    {
        resolvedPath = "";
        if (token.Contains('/', StringComparison.Ordinal))
        {
            string explicitPath;
            try
            {
                explicitPath = Path.GetFullPath(token);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                return false;
            }

            if (IsUnixExecutable(explicitPath))
            {
                resolvedPath = explicitPath;
                return true;
            }

            return false;
        }

        foreach (var entry in pathEntries)
        {
            if (!TryNormalizeRootedPathEntry(entry, out var directory))
                continue;

            var candidate = Path.Combine(directory, token);
            if (!IsUnixExecutable(candidate))
                continue;

            resolvedPath = Path.GetFullPath(candidate);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Splits PATH into entries without treating an empty entry as the current directory.
    /// </summary>
    /// <param name="value">The PATH value.</param>
    /// <returns>The nonempty PATH entries in their original order.</returns>
    internal static IReadOnlyList<string> SplitPathEntries(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return [];

        return value.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Splits and normalizes PATHEXT while retaining only supported editor target types.
    /// </summary>
    /// <param name="value">The PATHEXT value, or null to use the Windows default.</param>
    /// <returns>The normalized supported extensions in search order.</returns>
    internal static IReadOnlyList<string> SplitPathExtensions(string? value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? DefaultPathExtensions : value;
        var extensions = new List<string>();
        foreach (var item in source.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var extension = item.StartsWith('.') ? item : $".{item}";
            if (!IsSupportedWindowsExtension(extension)
                || extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            extensions.Add(extension.ToUpperInvariant());
        }

        return extensions;
    }

    private static bool ContainsWindowsDirectorySeparator(string value) =>
        value.Contains('\\', StringComparison.Ordinal)
        || value.Contains('/', StringComparison.Ordinal);

    private static bool IsSupportedWindowsExtension(string extension) =>
        extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".com", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnixExecutable(string path)
    {
        if (!File.Exists(path) || OperatingSystem.IsWindows())
            return false;

        try
        {
            var mode = File.GetUnixFileMode(path);
            const UnixFileMode executeModes =
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (mode & executeModes) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryNormalizeRootedPathEntry(string value, out string directory)
    {
        directory = value.Trim();
        if (directory.Length >= 2
            && directory[0] == '"'
            && directory[^1] == '"')
        {
            directory = directory[1..^1];
        }

        if (directory.Length == 0 || !Path.IsPathFullyQualified(directory))
            return false;

        try
        {
            directory = Path.GetFullPath(directory);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            directory = "";
            return false;
        }
    }

    private static bool TryWindowsCandidate(
        string candidate,
        IReadOnlyList<string> pathExtensions,
        out string resolvedPath)
    {
        resolvedPath = "";
        if (Path.HasExtension(candidate))
            return TryExistingWindowsFile(candidate, out resolvedPath);

        foreach (var extension in pathExtensions)
        {
            if (TryExistingWindowsFile(candidate + extension, out resolvedPath))
                return true;
        }

        return false;
    }

    private static bool TryExistingWindowsFile(string candidate, out string resolvedPath)
    {
        resolvedPath = "";
        if (!IsSupportedWindowsExtension(Path.GetExtension(candidate)) || !File.Exists(candidate))
            return false;

        resolvedPath = Path.GetFullPath(candidate);
        return true;
    }
}
