using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// The resolved ReadyToRun view of one image: its precompiled methods, the analyzer whose bytes hold
/// their native code (self, or the owner composite for a component), the metadata providers that back
/// the managed tree (self for non-composite, the resolved component assemblies for a composite), the
/// analyzers owned for the lifetime of the root, and the component listing. A single object so the
/// analyzer resolves composite structure once.
/// </summary>
/// <param name="Methods">The precompiled method entries joined to their code ranges.</param>
/// <param name="CodeImage">The analyzer whose bytes hold the native code.</param>
/// <param name="MetadataProviders">MVID → the analyzer whose metadata backs that assembly's methods.</param>
/// <param name="Owned">Sibling analyzers opened for resolution; disposed with the root.</param>
/// <param name="Components">The component listing (composite only), each with its resolution state.</param>
/// <param name="OwnerCompositeMissing">Whether this is a component whose owner composite is not on disk.</param>
/// <param name="Diagnostic">A human-readable note when resolution is incomplete, or null.</param>
internal sealed record ReadyToRunModel(
    IReadOnlyList<ReadyToRunMethodEntry> Methods,
    AssemblyAnalyzer CodeImage,
    IReadOnlyDictionary<Guid, AssemblyAnalyzer> MetadataProviders,
    IReadOnlyList<AssemblyAnalyzer> Owned,
    IReadOnlyList<ReadyToRunComponent> Components,
    bool OwnerCompositeMissing,
    string? Diagnostic);
