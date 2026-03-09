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
    int StrongNameSignatureSize);
