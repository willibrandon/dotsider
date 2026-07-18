using Dotsider.Core.Analysis.Models;
using System.IO.Compression;
using System.Xml.Linq;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Opens and analyzes a NuGet package (.nupkg) file.
/// Reads package metadata from .nuspec and lists all contents.
/// </summary>
public sealed class NuGetPackageAnalyzer : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly Dictionary<NuGetFileEntry, ZipArchiveEntry> _archiveEntries =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<NuGetFileEntry> _dllEntries =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<NuGetFileEntry, string> _extractedPaths =
        new(ReferenceEqualityComparer.Instance);
    private readonly Lock _gate = new();
    private Dictionary<NuGetFileEntry, string>? _destinationPaths;
    private HashSet<NuGetFileEntry>? _unsafeEntries;
    private string? _tempDirectory;
    private int _topologyDestinationCount;
    private bool _disposed;

    /// <summary>
    /// Opens and analyzes the specified NuGet package file.
    /// </summary>
    /// <param name="nupkgPath">Path to the .nupkg file.</param>
    public NuGetPackageAnalyzer(string nupkgPath)
    {
        FilePath = nupkgPath;
        FileName = Path.GetFileName(nupkgPath);
        _archive = ZipFile.OpenRead(nupkgPath);

        try
        {
            ReadNuspec();
            BuildFileList();
        }
        catch
        {
            _archive.Dispose();
            throw;
        }
    }

    /// <summary>The full path to the .nupkg file.</summary>
    public string FilePath { get; }

    /// <summary>The file name of the .nupkg file.</summary>
    public string FileName { get; }

    /// <summary>
    /// The raw, untrusted NuGet package ID from the .nuspec manifest, or null. This value is not
    /// display-safe.
    /// </summary>
    public string? PackageId { get; private set; }

    /// <summary>
    /// The raw, untrusted package version from the .nuspec manifest, or null. This value is not
    /// display-safe.
    /// </summary>
    public string? PackageVersion { get; private set; }

    /// <summary>
    /// The raw, untrusted package authors from the .nuspec manifest, or null. This value is not
    /// display-safe.
    /// </summary>
    public string? Authors { get; private set; }

    /// <summary>
    /// The raw, untrusted package description from the .nuspec manifest, or null. This value is
    /// not display-safe.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>All files in the package.</summary>
    public IReadOnlyList<NuGetFileEntry> Files { get; private set; } = [];

    /// <summary>Only the DLL files in the package.</summary>
    public IReadOnlyList<NuGetFileEntry> DllFiles { get; private set; } = [];

    /// <summary>
    /// Gets the private extraction directory, or <see langword="null"/> before extraction is
    /// required.
    /// </summary>
    internal string? ExtractionDirectory
    {
        get
        {
            lock (_gate)
                return _tempDirectory;
        }
    }

    /// <summary>
    /// Gets the number of unique canonical destinations examined for file-versus-directory
    /// conflicts in the current extraction plan.
    /// </summary>
    internal int TopologyDestinationCount
    {
        get
        {
            lock (_gate)
                return _topologyDestinationCount;
        }
    }

    /// <summary>
    /// Extracts a DLL from the package into a private temporary directory and creates an analyzer.
    /// </summary>
    /// <param name="entry">
    /// The exact <see cref="NuGetFileEntry"/> instance returned by this analyzer.
    /// </param>
    /// <returns>
    /// An analyzer for the selected DLL. Dispose it before disposing this package analyzer.
    /// </returns>
    /// <remarks>
    /// Dispose the returned analyzer before disposing this package analyzer so that its temporary
    /// extraction directory can be removed.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entry"/> was not returned by this analyzer or does not represent a DLL.
    /// </exception>
    /// <exception cref="UnsafePackageEntryException">
    /// The package entry has an unsafe or ambiguous extraction path.
    /// </exception>
    /// <exception cref="ObjectDisposedException">This analyzer has been disposed.</exception>
    /// <exception cref="IOException">The DLL could not be extracted or read.</exception>
    /// <exception cref="BadImageFormatException">The extracted file has an invalid format.</exception>
    public AssemblyAnalyzer OpenDll(NuGetFileEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_archiveEntries.TryGetValue(entry, out var archiveEntry) ||
                !_dllEntries.Contains(entry))
            {
                throw new ArgumentException(
                    "The entry was not returned by this analyzer or does not represent a DLL.",
                    nameof(entry));
            }

            if (!ContainedPathResolver.IsSafeRelativePath(archiveEntry.FullName))
                throw new UnsafePackageEntryException();

            EnsureExtractionPlan();

            if (_unsafeEntries!.Contains(entry) ||
                !_destinationPaths!.TryGetValue(entry, out var destinationPath))
            {
                throw new UnsafePackageEntryException();
            }

            if (_extractedPaths.TryGetValue(entry, out var extractedPath))
                return new AssemblyAnalyzer(extractedPath);

            return ExtractDll(entry, archiveEntry, destinationPath);
        }
    }

    private AssemblyAnalyzer ExtractDll(
        NuGetFileEntry entry,
        ZipArchiveEntry archiveEntry,
        string destinationPath)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(destinationDirectory);

        AssemblyAnalyzer? analyzer = null;
        var destinationCreated = false;

        try
        {
            using (var source = archiveEntry.Open())
            using (var target = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                destinationCreated = true;
                source.CopyTo(target);
            }

            analyzer = new AssemblyAnalyzer(destinationPath);
            _extractedPaths.Add(entry, destinationPath);
            return analyzer;
        }
        catch
        {
            analyzer?.Dispose();
            if (destinationCreated)
                TryDeleteFile(destinationPath);
            throw;
        }
    }

    private void EnsureExtractionPlan()
    {
        if (_destinationPaths is not null)
            return;

        var tempDirectory = Directory.CreateTempSubdirectory("dotsider-").FullName;
        try
        {
            var destinationPaths = new Dictionary<NuGetFileEntry, string>(
                ReferenceEqualityComparer.Instance);
            var destinationOwners = new Dictionary<string, NuGetFileEntry>(
                ContainedPathResolver.PathComparer);
            var unsafeEntries = new HashSet<NuGetFileEntry>(ReferenceEqualityComparer.Instance);

            foreach (var entry in _dllEntries)
            {
                var archiveEntry = _archiveEntries[entry];
                if (!ContainedPathResolver.TryResolve(
                    tempDirectory,
                    archiveEntry.FullName,
                    out var destinationPath))
                {
                    unsafeEntries.Add(entry);
                    continue;
                }

                destinationPaths.Add(entry, destinationPath);
                var destinationKey = ContainedPathResolver.GetComparisonKey(destinationPath);
                if (destinationOwners.TryGetValue(destinationKey, out var existingEntry))
                {
                    unsafeEntries.Add(existingEntry);
                    unsafeEntries.Add(entry);
                }
                else
                {
                    destinationOwners.Add(destinationKey, entry);
                }
            }

            var topologyDestinationCount = MarkTopologyConflicts(
                destinationOwners,
                unsafeEntries);

            _tempDirectory = tempDirectory;
            _destinationPaths = destinationPaths;
            _unsafeEntries = unsafeEntries;
            _topologyDestinationCount = topologyDestinationCount;
        }
        catch
        {
            TryDeleteDirectory(tempDirectory);
            throw;
        }
    }

    private static int MarkTopologyConflicts(
        Dictionary<string, NuGetFileEntry> destinationOwners,
        HashSet<NuGetFileEntry> unsafeEntries)
    {
        var destinations = new ContainedPathTrie(ContainedPathResolver.PathComparer);
        foreach (var (destinationPath, entry) in destinationOwners)
            destinations.Add(destinationPath, entry, unsafeEntries);

        return destinationOwners.Count;
    }

    private void ReadNuspec()
    {
        var nuspecEntry = _archive.Entries.FirstOrDefault(e =>
            e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

        if (nuspecEntry is null)
            return;

        using var stream = nuspecEntry.Open();
        var doc = XDocument.Load(stream);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var metadata = doc.Root?.Element(ns + "metadata");

        PackageId = metadata?.Element(ns + "id")?.Value;
        PackageVersion = metadata?.Element(ns + "version")?.Value;
        Authors = metadata?.Element(ns + "authors")?.Value;
        Description = metadata?.Element(ns + "description")?.Value;
    }

    private void BuildFileList()
    {
        var files = new List<NuGetFileEntry>();
        var dlls = new List<NuGetFileEntry>();

        foreach (var archiveEntry in _archive.Entries)
        {
            var fullPath = archiveEntry.FullName;
            var separatorIndex = Math.Max(fullPath.LastIndexOf('/'), fullPath.LastIndexOf('\\'));
            var name = fullPath[(separatorIndex + 1)..];
            if (name.Length == 0)
                continue;

            var isDll = name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            var directory = separatorIndex < 0
                ? string.Empty
                : fullPath[..separatorIndex].Replace('\\', '/');
            var entry = new NuGetFileEntry(
                name,
                fullPath,
                directory,
                archiveEntry.CompressedLength,
                archiveEntry.Length,
                isDll);

            _archiveEntries.Add(entry, archiveEntry);
            files.Add(entry);
            if (isDll)
            {
                _dllEntries.Add(entry);
                dlls.Add(entry);
            }
        }

        Files = [.. files.OrderBy(static file => file.FullPath, StringComparer.Ordinal)];
        DllFiles = [.. dlls.OrderBy(static file => file.FullPath, StringComparer.Ordinal)];
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                _archive.Dispose();
            }
            finally
            {
                if (_tempDirectory is not null)
                    TryDeleteDirectory(_tempDirectory);
                GC.SuppressFinalize(this);
            }
        }
    }
}
