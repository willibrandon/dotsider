using System.Reflection.PortableExecutable;

namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// CLR (Common Language Runtime) header information from the PE file's COR20 header.
/// </summary>
/// <param name="MajorRuntimeVersion">Major version of the CLR required.</param>
/// <param name="MinorRuntimeVersion">Minor version of the CLR required.</param>
/// <param name="MetadataRva">RVA of the metadata directory.</param>
/// <param name="MetadataSize">Size of the metadata directory in bytes.</param>
/// <param name="Flags">CLR header flags (ILOnly, 32BitRequired, StrongNameSigned, etc.).</param>
/// <param name="EntryPointToken">Metadata token of the entry point method, or zero.</param>
/// <param name="ResourcesRva">RVA of the managed resources directory.</param>
/// <param name="ResourcesSize">Size of the managed resources directory.</param>
/// <param name="StrongNameSignatureRva">RVA of the strong name signature.</param>
/// <param name="StrongNameSignatureSize">Size of the strong name signature.</param>
/// <param name="ManagedNativeHeader">
/// The managed native header directory. Non-empty for precompiled images: a crossgen2 ReadyToRun
/// image points it at the <c>READYTORUN_HEADER</c>. Empty (<c>Size == 0</c>) for a plain managed assembly.
/// </param>
public sealed record ClrHeader(
    int MajorRuntimeVersion,
    int MinorRuntimeVersion,
    int MetadataRva,
    int MetadataSize,
    CorFlags Flags,
    int EntryPointToken,
    int ResourcesRva,
    int ResourcesSize,
    int StrongNameSignatureRva,
    int StrongNameSignatureSize,
    DirectoryEntry ManagedNativeHeader)
{
    /// <summary>
    /// Constructs a header without a managed native header directory. Preserves the original
    /// ten-argument shape for callers written before <see cref="ManagedNativeHeader"/> was added.
    /// </summary>
    public ClrHeader(
        int majorRuntimeVersion,
        int minorRuntimeVersion,
        int metadataRva,
        int metadataSize,
        CorFlags flags,
        int entryPointToken,
        int resourcesRva,
        int resourcesSize,
        int strongNameSignatureRva,
        int strongNameSignatureSize)
        : this(majorRuntimeVersion, minorRuntimeVersion, metadataRva, metadataSize, flags,
            entryPointToken, resourcesRva, resourcesSize, strongNameSignatureRva,
            strongNameSignatureSize, default)
    {
    }

    /// <summary>
    /// Deconstructs the original ten fields, preserving the pre-<see cref="ManagedNativeHeader"/>
    /// positional shape for existing deconstruction sites.
    /// </summary>
    public void Deconstruct(
        out int majorRuntimeVersion,
        out int minorRuntimeVersion,
        out int metadataRva,
        out int metadataSize,
        out CorFlags flags,
        out int entryPointToken,
        out int resourcesRva,
        out int resourcesSize,
        out int strongNameSignatureRva,
        out int strongNameSignatureSize)
    {
        majorRuntimeVersion = MajorRuntimeVersion;
        minorRuntimeVersion = MinorRuntimeVersion;
        metadataRva = MetadataRva;
        metadataSize = MetadataSize;
        flags = Flags;
        entryPointToken = EntryPointToken;
        resourcesRva = ResourcesRva;
        resourcesSize = ResourcesSize;
        strongNameSignatureRva = StrongNameSignatureRva;
        strongNameSignatureSize = StrongNameSignatureSize;
    }
}
