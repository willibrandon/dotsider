using System.Security.Cryptography;

namespace Dotsider.DeployHost;

/// <summary>
/// Creates and verifies the sample payload SHA-256 manifest.
/// The text format remains compatible with the existing deployment artifact.
/// Symlinks and paths escaping the sample root are never followed.
/// </summary>
internal static class SampleManifest
{
    private const int Sha256HexLength = 64;

    /// <summary>
    /// Hashes regular files beneath the sample directory and writes sorted entries.
    /// Each line uses the existing lower-case GNU-style digest and relative path shape.
    /// The completed manifest replaces its destination atomically.
    /// </summary>
    /// <param name="sampleDirectory">The fixed sample directory.</param>
    /// <param name="manifestPath">The manifest destination.</param>
    /// <param name="cancellationToken">Stops hashing and writing.</param>
    internal static async Task CreateAsync(
        string sampleDirectory,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        string sampleRoot = Path.GetFullPath(sampleDirectory);
        var entries = new List<SampleManifestEntry>();
        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
            RecurseSubdirectories = true,
        };
        foreach (string path in Directory.EnumerateFiles(sampleRoot, "*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = Path.GetRelativePath(sampleRoot, path).Replace('\\', '/');
            ValidateRelativePath(relativePath);
            await using FileStream stream = File.OpenRead(path);
            byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            entries.Add(new SampleManifestEntry("./" + relativePath, Convert.ToHexStringLower(digest)));
        }

        entries.Sort(static (left, right) => CompareUtf8(left.RelativePath, right.RelativePath));
        string temporaryPath = manifestPath + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var writer = new StreamWriter(temporaryPath, append: false, new System.Text.UTF8Encoding(false)))
            {
                foreach (SampleManifestEntry entry in entries)
                {
                    await writer.WriteLineAsync($"{entry.Sha256}  {entry.RelativePath}").ConfigureAwait(false);
                }

                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Verifies every manifest entry against the current sample payload.
    /// Missing, malformed, additional, or changed regular files fail verification.
    /// No path outside the sample root is opened.
    /// </summary>
    /// <param name="sampleDirectory">The fixed sample directory.</param>
    /// <param name="manifestPath">The manifest to verify.</param>
    /// <param name="cancellationToken">Stops hashing.</param>
    /// <returns>Whether the manifest exactly matches the sample payload.</returns>
    internal static async Task<bool> VerifyAsync(
        string sampleDirectory,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        string sampleRoot = Path.GetFullPath(sampleDirectory);
        var expectedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in await File.ReadAllLinesAsync(manifestPath, cancellationToken).ConfigureAwait(false))
        {
            if (line.Length < Sha256HexLength + 3
                || line[Sha256HexLength] != ' '
                || line[Sha256HexLength + 1] != ' ')
            {
                return false;
            }

            string expectedDigest = line[..Sha256HexLength];
            if (!expectedDigest.All(Uri.IsHexDigit))
            {
                return false;
            }

            string manifestPathValue = line[(Sha256HexLength + 2)..];
            string relativePath = manifestPathValue.StartsWith("./", StringComparison.Ordinal)
                ? manifestPathValue[2..]
                : manifestPathValue;
            try
            {
                ValidateRelativePath(relativePath);
            }
            catch (InvalidDataException)
            {
                return false;
            }

            string fullPath = Path.GetFullPath(Path.Combine(sampleRoot, relativePath));
            if (!IsWithinRoot(sampleRoot, fullPath)
                || !File.Exists(fullPath)
                || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0
                || !expectedPaths.Add(relativePath))
            {
                return false;
            }

            await using FileStream stream = File.OpenRead(fullPath);
            byte[] actualDigest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            if (!Convert.ToHexStringLower(actualDigest).Equals(expectedDigest, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
            RecurseSubdirectories = true,
        };
        int actualCount = Directory.EnumerateFiles(sampleRoot, "*", options).Count();
        return expectedPaths.Count == actualCount;
    }

    private static void ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathFullyQualified(relativePath)
            || relativePath.Split(['/', '\\']).Contains("..", StringComparer.Ordinal)
            || relativePath.Contains('\0'))
        {
            throw new InvalidDataException($"Unsafe sample manifest path '{relativePath}'.");
        }
    }

    private static bool IsWithinRoot(string root, string path)
    {
        string rootWithSeparator = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }

    private static int CompareUtf8(string left, string right)
    {
        byte[] leftBytes = System.Text.Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = System.Text.Encoding.UTF8.GetBytes(right);
        return leftBytes.AsSpan().SequenceCompareTo(rightBytes);
    }
}
