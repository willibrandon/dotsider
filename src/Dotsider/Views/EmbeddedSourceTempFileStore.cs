using System.Runtime.Versioning;

namespace Dotsider.Views;

/// <summary>
/// Materializes untrusted embedded source in a private session directory using inert,
/// collision-resistant file names.
/// </summary>
internal sealed class EmbeddedSourceTempFileStore : IDisposable
{
    private const int MaximumNameLength = 64;
    private const int MaximumWriteAttempts = 10;
    private const string TempDirectoryPrefix = "dotsider-embedded-source-";

    private static readonly UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private static readonly UnixFileMode PrivateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly Lock _gate = new();
    private string? _directoryPath;
    private bool _disposed;

    /// <summary>
    /// Initializes a store and registers best-effort process-exit cleanup.
    /// </summary>
    internal EmbeddedSourceTempFileStore()
    {
        AppDomain.CurrentDomain.ProcessExit += HandleProcessExit;
    }

    /// <summary>
    /// Gets the private session directory after it has been created.
    /// </summary>
    internal string? SessionDirectory
    {
        get
        {
            lock (_gate)
                return _directoryPath;
        }
    }

    /// <summary>
    /// Builds an inert temporary filename for an untrusted metadata document name.
    /// </summary>
    /// <param name="methodName">The method name used when the document has no usable name.</param>
    /// <param name="documentPath">The untrusted portable PDB document path.</param>
    /// <returns>A bounded filename containing a GUID and a safe extension.</returns>
    internal static string BuildFileName(string methodName, string documentPath) =>
        BuildFileName(methodName, documentPath, Guid.NewGuid());

    /// <summary>
    /// Builds an inert temporary filename using a caller-supplied uniqueness value.
    /// </summary>
    /// <param name="methodName">The method name used when the document has no usable name.</param>
    /// <param name="documentPath">The untrusted portable PDB document path.</param>
    /// <param name="uniqueId">The uniqueness value to include in the filename.</param>
    /// <returns>A bounded filename containing the supplied GUID and a safe extension.</returns>
    internal static string BuildFileName(string methodName, string documentPath, Guid uniqueId)
    {
        var segment = GetDocumentSegment(documentPath);
        var dotIndex = segment.LastIndexOf('.');
        var name = dotIndex >= 0 ? segment[..dotIndex] : segment;

        if (!TrySanitizeNamePart(name, out var sanitizedName)
            && !TrySanitizeNamePart(methodName.AsSpan(), out sanitizedName))
        {
            sanitizedName = "source";
        }

        return $"{sanitizedName}-{uniqueId:N}{SanitizeExtension(segment, dotIndex)}";
    }

    /// <summary>
    /// Replaces characters outside the filename allowlist and bounds the result length.
    /// </summary>
    /// <param name="value">The untrusted name component.</param>
    /// <returns>An ASCII-only filename component no longer than 64 characters.</returns>
    internal static string SanitizeNamePart(string value) => SanitizeNamePart(value.AsSpan());

    /// <summary>
    /// Returns the canonical safe extension for an untrusted document path.
    /// </summary>
    /// <param name="documentPath">The untrusted portable PDB document path.</param>
    /// <returns>An allowlisted lowercase extension, or <c>.txt</c>.</returns>
    internal static string SanitizeExtension(string documentPath)
    {
        var segment = GetDocumentSegment(documentPath);
        return SanitizeExtension(segment, segment.LastIndexOf('.'));
    }

    /// <summary>
    /// Writes embedded source bytes to a new file inside the private session directory.
    /// </summary>
    /// <param name="methodName">The method name used when the document has no usable name.</param>
    /// <param name="documentPath">The untrusted portable PDB document path.</param>
    /// <param name="bytes">The embedded source bytes.</param>
    /// <returns>The absolute path of the newly created source file.</returns>
    internal string Write(string methodName, string documentPath, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(methodName);
        ArgumentNullException.ThrowIfNull(documentPath);
        ArgumentNullException.ThrowIfNull(bytes);

        var directory = GetOrCreateDirectory();
        for (var attempt = 0; attempt < MaximumWriteAttempts; attempt++)
        {
            var path = Path.Combine(directory, BuildFileName(methodName, documentPath));
            try
            {
                using var stream = new FileStream(path, CreateFileOptions());
                stream.Write(bytes);
                return path;
            }
            catch (IOException) when (File.Exists(path))
            {
                // An extraordinarily unlikely GUID collision. Generate another name.
            }
        }

        throw new IOException("Could not create a unique embedded-source temporary file.");
    }

    /// <summary>
    /// Moves an owned source file to a <c>.txt</c> path for system-association dispatch.
    /// </summary>
    /// <param name="path">The source path created by this store.</param>
    /// <returns>The original path when already text, otherwise the new text path.</returns>
    internal string PrepareAssociationPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = GetOrCreateDirectory();
        var fullPath = Path.GetFullPath(path);
        var pathDirectory = Path.GetDirectoryName(fullPath);
        if (!string.Equals(
                directory,
                pathDirectory,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new IOException("The embedded-source path is outside the private session directory.");
        }

        if (string.Equals(Path.GetExtension(fullPath), ".txt", StringComparison.OrdinalIgnoreCase))
            return fullPath;

        var textPath = Path.ChangeExtension(fullPath, ".txt");
        File.Move(fullPath, textPath);
        return textPath;
    }

    /// <summary>
    /// Deletes this store's private session directory when possible.
    /// </summary>
    public void Dispose()
    {
        string? directory;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            directory = _directoryPath;
            _directoryPath = null;
        }

        AppDomain.CurrentDomain.ProcessExit -= HandleProcessExit;
        DeleteDirectory(directory);
    }

    private static FileStreamOptions CreateFileOptions()
    {
        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.CreateNew,
            Share = FileShare.Read
        };

        if (!OperatingSystem.IsWindows())
            SetUnixCreateMode(options);

        return options;
    }

    private static void DeleteDirectory(string? directory)
    {
        if (directory is null)
            return;

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static ReadOnlySpan<char> GetDocumentSegment(string documentPath)
    {
        var path = documentPath.AsSpan();
        var slash = path.LastIndexOf('/');
        var backslash = path.LastIndexOf('\\');
        var separator = Math.Max(slash, backslash);
        return separator >= 0 ? path[(separator + 1)..] : path;
    }

    private static bool IsAllowedNameCharacter(char value) =>
        value is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '_'
            or '-';

    private static string SanitizeExtension(ReadOnlySpan<char> segment, int dotIndex)
    {
        if (dotIndex < 0)
            return ".txt";

        var extension = segment[dotIndex..];
        if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            return ".cs";
        if (extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase))
            return ".cshtml";
        if (extension.Equals(".fs", StringComparison.OrdinalIgnoreCase))
            return ".fs";
        if (extension.Equals(".fsi", StringComparison.OrdinalIgnoreCase))
            return ".fsi";
        if (extension.Equals(".il", StringComparison.OrdinalIgnoreCase))
            return ".il";
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            return ".json";
        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
            return ".md";
        if (extension.Equals(".razor", StringComparison.OrdinalIgnoreCase))
            return ".razor";
        if (extension.Equals(".resx", StringComparison.OrdinalIgnoreCase))
            return ".resx";
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return ".txt";
        if (extension.Equals(".vb", StringComparison.OrdinalIgnoreCase))
            return ".vb";
        if (extension.Equals(".vbhtml", StringComparison.OrdinalIgnoreCase))
            return ".vbhtml";
        if (extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase))
            return ".xaml";
        if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            return ".xml";

        return ".txt";
    }

    private static string SanitizeNamePart(ReadOnlySpan<char> value)
    {
        var length = Math.Min(value.Length, MaximumNameLength);
        Span<char> buffer = stackalloc char[length];
        _ = FillSanitizedName(value, buffer);

        return new string(buffer);
    }

    private static bool TrySanitizeNamePart(
        ReadOnlySpan<char> value,
        out string sanitizedName)
    {
        var length = Math.Min(value.Length, MaximumNameLength);
        Span<char> buffer = stackalloc char[length];
        if (!FillSanitizedName(value, buffer))
        {
            sanitizedName = "";
            return false;
        }

        sanitizedName = new string(buffer);
        return true;
    }

    private static bool FillSanitizedName(
        ReadOnlySpan<char> value,
        Span<char> destination)
    {
        var containsAllowedCharacter = false;
        for (var index = 0; index < destination.Length; index++)
        {
            var character = value[index];
            if (IsAllowedNameCharacter(character))
            {
                destination[index] = character;
                containsAllowedCharacter = true;
            }
            else
            {
                destination[index] = '_';
            }
        }

        return containsAllowedCharacter;
    }

    [UnsupportedOSPlatform("windows")]
    private static void SetUnixCreateMode(FileStreamOptions options) =>
        options.UnixCreateMode = PrivateFileMode;

    private string GetOrCreateDirectory()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_directoryPath is not null)
                return _directoryPath;

            var directory = Directory.CreateTempSubdirectory(TempDirectoryPrefix).FullName;
            try
            {
                if (!OperatingSystem.IsWindows())
                    SetUnixDirectoryMode(directory);
            }
            catch
            {
                DeleteDirectory(directory);
                throw;
            }

            _directoryPath = directory;
            return directory;
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static void SetUnixDirectoryMode(string directory) =>
        File.SetUnixFileMode(directory, PrivateDirectoryMode);

    private void HandleProcessExit(object? sender, EventArgs eventArgs)
    {
        string? directory;
        lock (_gate)
            directory = _directoryPath;

        DeleteDirectory(directory);
    }
}
