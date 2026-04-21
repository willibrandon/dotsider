using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Disposable scope for writing minimal synthetic PE assemblies into a temp directory so
/// tests can probe them from disk the same way the graph builder probes a real referencing
/// assembly. Supports emitting AssemblyRefs with explicit version and public key token, and
/// TypeRefs whose resolution scope points at one of those AssemblyRefs so duplicate-name
/// scenarios can exercise the per-identity TypeRef count path.
/// </summary>
internal sealed class SyntheticAssemblyScope : IDisposable
{
    /// <summary>The scope's temp directory.</summary>
    public string Directory { get; }

    private SyntheticAssemblyScope(string dir) { Directory = dir; }

    /// <summary>Creates a new temp directory scope.</summary>
    public static SyntheticAssemblyScope Create()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dotsider-depgraph-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        return new SyntheticAssemblyScope(dir);
    }

    /// <summary>
    /// Writes a minimal synthetic managed PE with the given name, version, and AssemblyRefs.
    /// </summary>
    /// <param name="name">Assembly simple name (also used as the file name).</param>
    /// <param name="version">Assembly version, defaulting to 1.0.0.0 when null.</param>
    /// <param name="refs">AssemblyRefs to emit, each as (name, version).</param>
    /// <param name="typeRefs">TypeRefs keyed to <paramref name="refs"/> by index.</param>
    /// <returns>The full path to the written assembly.</returns>
    public string WriteAssembly(
        string name,
        Version? version = null,
        IReadOnlyList<(string Name, Version Version)>? refs = null,
        IReadOnlyList<(string FullName, int RefIndex)>? typeRefs = null)
    {
        var bytes = BuildAssembly(
            name, version ?? new Version(1, 0, 0, 0),
            refs is null
                ? []
                : [.. refs.Select(r => (r.Name, r.Version, (byte[]?)null))],
            typeRefs ?? []);
        var path = Path.Combine(Directory, $"{name}.dll");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>
    /// Writes a minimal synthetic managed PE with AssemblyRefs carrying explicit public key tokens.
    /// </summary>
    /// <param name="name">Assembly simple name.</param>
    /// <param name="refs">AssemblyRefs to emit, each as (name, version, pkt).</param>
    /// <returns>The full path to the written assembly.</returns>
    public string WriteAssembly(
        string name,
        IReadOnlyList<(string Name, Version Version, byte[]? PublicKeyToken)> refs)
    {
        var bytes = BuildAssembly(
            name, new Version(1, 0, 0, 0),
            [.. refs],
            []);
        var path = Path.Combine(Directory, $"{name}.dll");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); }
        catch { }
    }

    private static byte[] BuildAssembly(
        string moduleName,
        Version version,
        IReadOnlyList<(string Name, Version Version, byte[]? PublicKeyToken)> refs,
        IReadOnlyList<(string FullName, int RefIndex)> typeRefs)
    {
        var metadata = new MetadataBuilder();
        metadata.AddAssembly(
            metadata.GetOrAddString(moduleName),
            version,
            default, default,
            0, AssemblyHashAlgorithm.None);
        metadata.AddModule(
            0,
            metadata.GetOrAddString(moduleName + ".dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default, default);
        metadata.AddTypeDefinition(
            default, default,
            metadata.GetOrAddString("<Module>"),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));

        var asmRefHandles = new List<AssemblyReferenceHandle>();
        foreach (var r in refs)
        {
            var pktBlob = r.PublicKeyToken is null
                ? default
                : metadata.GetOrAddBlob(r.PublicKeyToken);
            asmRefHandles.Add(metadata.AddAssemblyReference(
                metadata.GetOrAddString(r.Name),
                r.Version,
                default,
                pktBlob,
                0,
                default));
        }

        foreach (var (fullName, idx) in typeRefs)
        {
            var dot = fullName.LastIndexOf('.');
            var ns = dot >= 0 ? fullName[..dot] : string.Empty;
            var n = dot >= 0 ? fullName[(dot + 1)..] : fullName;
            metadata.AddTypeReference(
                asmRefHandles[idx],
                metadata.GetOrAddString(ns),
                metadata.GetOrAddString(n));
        }

        var pe = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream: new BlobBuilder());
        var blob = new BlobBuilder();
        pe.Serialize(blob);
        return blob.ToArray();
    }
}
