using System.Security;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves untrusted relative paths beneath a trusted directory.
/// </summary>
internal static class ContainedPathResolver
{
    private const int MaxSymbolicLinkDepth = 64;

    private static StringComparison PathComparison { get; } = OperatingSystem.IsWindows()
        || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparison PhysicalPathComparison { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// Gets the comparer used for canonical filesystem paths on the current platform.
    /// </summary>
    internal static StringComparer PathComparer { get; } = OperatingSystem.IsWindows()
        || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    /// <summary>
    /// Gets a canonical key for filesystem-equivalence comparisons on the current platform.
    /// </summary>
    /// <param name="path">The canonical filesystem path.</param>
    /// <returns>The platform comparison key.</returns>
    internal static string GetComparisonKey(string path) => OperatingSystem.IsMacOS()
        ? path.Normalize(NormalizationForm.FormD)
        : path;

    /// <summary>
    /// Determines whether a path has a portable, non-traversing relative form.
    /// </summary>
    /// <param name="relativePath">The untrusted relative path.</param>
    /// <returns>
    /// <see langword="true"/> when the path is relative and contains no parent traversal;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsSafeRelativePath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) ||
            IsDirectorySeparator(relativePath[0]) ||
            HasDrivePrefix(relativePath) ||
            ContainsInvalidCharacter(relativePath))
        {
            return false;
        }

        var segmentStart = 0;
        for (var index = 0; index <= relativePath.Length; index++)
        {
            if (index != relativePath.Length && !IsDirectorySeparator(relativePath[index]))
                continue;

            var segmentLength = index - segmentStart;
            if (segmentLength == 2 &&
                relativePath[segmentStart] == '.' &&
                relativePath[segmentStart + 1] == '.')
            {
                return false;
            }

            if (segmentLength > 0 &&
                !(segmentLength == 1 && relativePath[segmentStart] == '.') &&
                (relativePath[index - 1] is ' ' or '.' ||
                    IsWindowsDeviceName(relativePath.AsSpan(segmentStart, segmentLength))))
            {
                return false;
            }

            segmentStart = index + 1;
        }

        try
        {
            return !Path.IsPathRooted(NormalizeSeparators(relativePath));
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves an untrusted relative path and verifies that it is strictly beneath a trusted
    /// directory.
    /// </summary>
    /// <param name="rootDirectory">The trusted root directory.</param>
    /// <param name="relativePath">The untrusted relative path.</param>
    /// <param name="fullPath">The canonical contained path when resolution succeeds.</param>
    /// <returns>
    /// <see langword="true"/> when the path resolves beneath <paramref name="rootDirectory"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryResolve(
        string rootDirectory,
        string relativePath,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (!IsSafeRelativePath(relativePath))
            return false;

        try
        {
            var canonicalRoot = Path.GetFullPath(rootDirectory);
            var rootWithSeparator = Path.EndsInDirectorySeparator(canonicalRoot)
                ? canonicalRoot
                : string.Concat(canonicalRoot, Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(
                rootWithSeparator,
                NormalizeSeparators(relativePath)));

            if (!IsStrictDescendant(rootWithSeparator, candidate))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves an existing untrusted relative directory beneath a trusted directory, follows
    /// filesystem links, and verifies that the physical target remains contained.
    /// </summary>
    /// <param name="rootDirectory">The trusted root directory.</param>
    /// <param name="relativePath">The untrusted relative directory path.</param>
    /// <param name="fullPath">The canonical contained directory path when resolution succeeds.</param>
    /// <returns>
    /// <see langword="true"/> when the directory and its physical target are strictly beneath
    /// <paramref name="rootDirectory"/>; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryResolveExistingDirectory(
        string rootDirectory,
        string relativePath,
        out string fullPath) =>
        TryResolveExisting(rootDirectory, relativePath, isDirectory: true, out fullPath);

    /// <summary>
    /// Resolves an existing untrusted relative file beneath a trusted directory, follows
    /// filesystem links, and verifies that the physical target remains contained.
    /// </summary>
    /// <param name="rootDirectory">The trusted root directory.</param>
    /// <param name="relativePath">The untrusted relative file path.</param>
    /// <param name="fullPath">The canonical contained file path when resolution succeeds.</param>
    /// <returns>
    /// <see langword="true"/> when the file and its physical target are strictly beneath
    /// <paramref name="rootDirectory"/>; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryResolveExistingFile(
        string rootDirectory,
        string relativePath,
        out string fullPath) =>
        TryResolveExisting(rootDirectory, relativePath, isDirectory: false, out fullPath);

    /// <summary>
    /// Determines whether one canonical path is strictly beneath a canonical directory path.
    /// </summary>
    /// <param name="canonicalRoot">The canonical directory path, with or without a trailing separator.</param>
    /// <param name="canonicalCandidate">The canonical candidate path.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="canonicalCandidate"/> is a strict descendant
    /// of <paramref name="canonicalRoot"/>; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsStrictDescendant(string canonicalRoot, string canonicalCandidate)
    {
        var rootWithSeparator = Path.EndsInDirectorySeparator(canonicalRoot)
            ? canonicalRoot
            : string.Concat(canonicalRoot, Path.DirectorySeparatorChar);
        return canonicalCandidate.Length > rootWithSeparator.Length &&
            canonicalCandidate.StartsWith(rootWithSeparator, PathComparison);
    }

    private static bool HasDrivePrefix(string path) =>
        path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';

    private static bool ContainsInvalidCharacter(string path)
    {
        foreach (var character in path)
        {
            if (character is < ' ' or '\u007F' or ':' or '*' or '?' or '"' or '<' or '>' or '|')
                return true;
        }

        return false;
    }

    private static bool IsDirectorySeparator(char value) => value is '/' or '\\';

    private static bool IsPathException(Exception exception) =>
        exception is ArgumentException or IOException or NotSupportedException or SecurityException
            or UnauthorizedAccessException;

    private static bool IsWindowsDeviceName(ReadOnlySpan<char> segment)
    {
        var extensionIndex = segment.IndexOf('.');
        var name = extensionIndex < 0 ? segment : segment[..extensionIndex];
        return name.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) ||
            IsNumberedDeviceName(name, "COM") ||
            IsNumberedDeviceName(name, "LPT");
    }

    private static bool IsNumberedDeviceName(ReadOnlySpan<char> name, ReadOnlySpan<char> prefix) =>
        name.Length == prefix.Length + 1 &&
        name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
        name[^1] is (>= '1' and <= '9') or '\u00B9' or '\u00B2' or '\u00B3';

    private static string NormalizeSeparators(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static bool TryGetPhysicalPath(
        string path,
        bool isDirectory,
        out string physicalPath)
    {
        var remainingLinks = MaxSymbolicLinkDepth;
        return TryGetPhysicalPath(path, isDirectory, ref remainingLinks, out physicalPath);
    }

    private static bool TryGetPhysicalPath(
        string path,
        bool isDirectory,
        ref int remainingLinks,
        out string physicalPath)
    {
        physicalPath = string.Empty;
        var canonicalPath = Path.GetFullPath(path);
        var pathRoot = Path.GetPathRoot(canonicalPath);
        if (string.IsNullOrEmpty(pathRoot) || !Directory.Exists(pathRoot))
            return false;

        var currentPath = Path.GetFullPath(pathRoot);
        var relativePath = canonicalPath[pathRoot.Length..];
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < segments.Length; index++)
        {
            var entryPath = Path.Combine(currentPath, segments[index]);
            var entryIsDirectory = index < segments.Length - 1 || isDirectory;
            FileSystemInfo entry = entryIsDirectory
                ? new DirectoryInfo(entryPath)
                : new FileInfo(entryPath);
            if (!entry.Exists)
                return false;

            var target = entry.ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
            {
                if (remainingLinks-- == 0 ||
                    !TryGetPhysicalPath(
                        target.FullName,
                        entryIsDirectory,
                        ref remainingLinks,
                        out currentPath))
                {
                    return false;
                }

                continue;
            }

            currentPath = entry.FullName;
        }

        physicalPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentPath));
        return true;
    }

    private static bool TryResolveExisting(
        string rootDirectory,
        string relativePath,
        bool isDirectory,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (!TryResolve(rootDirectory, relativePath, out var candidatePath))
            return false;

        try
        {
            if (!TryGetPhysicalPath(rootDirectory, isDirectory: true, out var physicalRoot) ||
                !TryGetPhysicalPath(candidatePath, isDirectory, out var physicalCandidate) ||
                !IsStrictPhysicalDescendant(physicalRoot, physicalCandidate))
            {
                return false;
            }

            fullPath = candidatePath;
            return true;
        }
        catch (Exception exception) when (IsPathException(exception))
        {
            return false;
        }
    }

    private static bool IsStrictPhysicalDescendant(string physicalRoot, string physicalCandidate)
    {
        var rootWithSeparator = Path.EndsInDirectorySeparator(physicalRoot)
            ? physicalRoot
            : string.Concat(physicalRoot, Path.DirectorySeparatorChar);
        return physicalCandidate.Length > rootWithSeparator.Length &&
            physicalCandidate.StartsWith(rootWithSeparator, PhysicalPathComparison);
    }
}
