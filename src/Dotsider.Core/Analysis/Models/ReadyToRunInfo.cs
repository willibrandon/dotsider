namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The parsed facts about a PE image's crossgen2 ReadyToRun header. Present whenever an image
/// claims to be ReadyToRun (a managed native header directory or an <c>RTR_HEADER</c> export);
/// <see cref="Status"/> says whether it is usable, so a corrupt or unsupported image surfaces
/// its diagnostic rather than masquerading as plain managed.
/// </summary>
/// <param name="Signature">The header signature dword (expected <c>0x00525452</c>, "RTR\0").</param>
/// <param name="MajorVersion">The ReadyToRun major version.</param>
/// <param name="MinorVersion">The ReadyToRun minor version.</param>
/// <param name="Flags">The raw <c>ReadyToRunFlags</c> bitmask.</param>
/// <param name="IsComposite">Whether this image is a composite (its native code covers several component assemblies).</param>
/// <param name="IsComponent">Whether this image is a composite component (<c>READYTORUN_FLAG_COMPONENT</c>).</param>
/// <param name="IsPartialImage">Whether not every method is precompiled (<c>READYTORUN_FLAG_PARTIAL</c>) — a coverage flag, distinct from <see cref="Status"/>.</param>
/// <param name="HeaderRva">The RVA the header was located at.</param>
/// <param name="SectionCount">The number of rows in the section table.</param>
/// <param name="Status">The parse status.</param>
/// <param name="Diagnostic">A human-readable explanation when the status is not <see cref="ReadyToRunStatus.Valid"/>, otherwise null.</param>
/// <param name="Architecture">The image's real machine architecture.</param>
/// <param name="OwnerCompositeExecutable">For a component image, the filename of the composite that holds its native code, otherwise null.</param>
/// <param name="Sections">The section table rows.</param>
/// <param name="Components">The composite component assemblies, empty for a non-composite image.</param>
/// <param name="MethodCount">The number of MethodDef entry points.</param>
/// <param name="InstanceMethodCount">The number of instantiated-generic entry points.</param>
public sealed record ReadyToRunInfo(
    uint Signature,
    int MajorVersion,
    int MinorVersion,
    uint Flags,
    bool IsComposite,
    bool IsComponent,
    bool IsPartialImage,
    int HeaderRva,
    int SectionCount,
    ReadyToRunStatus Status,
    string? Diagnostic,
    NativeArchitecture Architecture,
    string? OwnerCompositeExecutable,
    IReadOnlyList<ReadyToRunSectionEntry> Sections,
    IReadOnlyList<ReadyToRunComponent> Components,
    int MethodCount,
    int InstanceMethodCount);
