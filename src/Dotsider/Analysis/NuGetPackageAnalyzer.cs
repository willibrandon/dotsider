using System.IO.Compression;
using System.Xml.Linq;
using Dotsider.Analysis.Models;

namespace Dotsider.Analysis;

/// <summary>
/// Opens and analyzes a NuGet package (.nupkg) file.
/// Reads package metadata from .nuspec and lists all contents.
/// </summary>
public sealed class NuGetPackageAnalyzer : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly string _tempDir;

    /// <summary>
    /// Opens and analyzes the specified NuGet package file.
    /// </summary>
    /// <param name="nupkgPath">Path to the .nupkg file.</param>
    public NuGetPackageAnalyzer(string nupkgPath)
    {
        FilePath = nupkgPath;
        FileName = Path.GetFileName(nupkgPath);
        _tempDir = Path.Combine(Path.GetTempPath(), "dotsider-" + Guid.NewGuid().ToString("N")[..8]);
        _archive = ZipFile.OpenRead(nupkgPath);
        ReadNuspec();
        BuildFileList();
    }

    /// <summary>The full path to the .nupkg file.</summary>
    public string FilePath { get; }

    /// <summary>The file name of the .nupkg file.</summary>
    public string FileName { get; }

    /// <summary>The NuGet package ID from the .nuspec manifest, or null.</summary>
    public string? PackageId { get; private set; }

    /// <summary>The package version from the .nuspec manifest, or null.</summary>
    public string? PackageVersion { get; private set; }

    /// <summary>The package authors from the .nuspec manifest, or null.</summary>
    public string? Authors { get; private set; }

    /// <summary>The package description from the .nuspec manifest, or null.</summary>
    public string? Description { get; private set; }

    /// <summary>All files in the package.</summary>
    public IReadOnlyList<NuGetFileEntry> Files { get; private set; } = [];

    /// <summary>Only the DLL files in the package.</summary>
    public IReadOnlyList<NuGetFileEntry> DllFiles { get; private set; } = [];

    /// <summary>
    /// Extracts a DLL from the package to a temp file and creates an AssemblyAnalyzer.
    /// </summary>
    public AssemblyAnalyzer OpenDll(NuGetFileEntry entry)
    {
        var tempPath = Path.Combine(_tempDir, entry.FullPath);
        var dir = Path.GetDirectoryName(tempPath)!;
        Directory.CreateDirectory(dir);

        var zipEntry = _archive.GetEntry(entry.FullPath);
        if (zipEntry is null)
            throw new FileNotFoundException($"Entry not found in package: {entry.FullPath}");

        using (var source = zipEntry.Open())
        using (var target = File.Create(tempPath))
        {
            source.CopyTo(target);
        }

        return new AssemblyAnalyzer(tempPath);
    }

    private void ReadNuspec()
    {
        var nuspecEntry = _archive.Entries.FirstOrDefault(e =>
            e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

        if (nuspecEntry is null) return;

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

        foreach (var entry in _archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // Skip directories

            var isDll = entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            var dir = Path.GetDirectoryName(entry.FullName)?.Replace('\\', '/') ?? "";

            var fileEntry = new NuGetFileEntry(
                entry.Name, entry.FullName, dir,
                entry.CompressedLength, entry.Length, isDll);

            files.Add(fileEntry);
            if (isDll) dlls.Add(fileEntry);
        }

        Files = files.OrderBy(f => f.FullPath).ToList();
        DllFiles = dlls.OrderBy(f => f.FullPath).ToList();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _archive.Dispose();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort cleanup */ }
    }
}
