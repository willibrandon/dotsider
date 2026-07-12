using Dotsider.Core.Analysis.Models;
using System.Reflection.Metadata;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Resolves a composite ReadyToRun <c>READYTORUN_FIXUP_ModuleOverride</c> index to the owning
/// component assembly. The index is into the manifest's assembly-reference table, which the manifest
/// builds in the same order as the <c>ComponentAssemblies</c> table — so index <c>i</c> maps to
/// component <c>i - offset</c>, where the offset is 1 or 2 depending on the R2R version (index 1 is
/// the manifest itself once component indices start at two, in major 6.3+). A cross-module method
/// token or fixup then resolves against that component's metadata rather than being left unnamed.
/// </summary>
internal sealed class ReadyToRunModuleContext
{
    /// <summary>An owning module resolved from an override index.</summary>
    /// <param name="AssemblyName">The component's simple name.</param>
    /// <param name="Mvid">The component's module version id.</param>
    /// <param name="Provider">The analyzer whose metadata backs the component, or null when unresolved.</param>
    internal readonly record struct ModuleRef(string AssemblyName, Guid Mvid, AssemblyAnalyzer? Provider);

    private readonly IReadOnlyList<ReadyToRunComponent> _components;
    private readonly Func<Guid, AssemblyAnalyzer?> _providerFor;
    private readonly int _offset;
    private readonly Guid? _systemModuleMvid;
    private MetadataReader? _systemMetadata;
    private bool _systemMetadataProbed;

    private ReadyToRunModuleContext(
        IReadOnlyList<ReadyToRunComponent> components, Func<Guid, AssemblyAnalyzer?> providerFor, int offset)
    {
        _components = components;
        _providerFor = providerFor;
        _offset = offset;
        foreach (var component in components)
        {
            if (string.Equals(
                    component.AssemblyName,
                    "System.Private.CoreLib",
                    StringComparison.Ordinal))
            {
                _systemModuleMvid = component.Mvid;
                break;
            }
        }
    }

    /// <summary>Builds a context from a composite's components and version, or null when not composite.</summary>
    public static ReadyToRunModuleContext? Create(
        ReadyToRunInfo info,
        IReadOnlyList<ReadyToRunComponent> components,
        Func<Guid, AssemblyAnalyzer?> providerFor)
    {
        if (!info.IsComposite || components.Count == 0)
            return null;
        return new ReadyToRunModuleContext(components, providerFor, IndexOffset(info));
    }

    /// <summary>Reconstructs the context for an already-resolved composite code image, or null.</summary>
    public static ReadyToRunModuleContext? ForImage(AssemblyAnalyzer image) =>
        image.ReadyToRunInfo is { IsComposite: true } info
            ? Create(info, image.ReadyToRunComponents, image.ReadyToRunMetadataProviderFor)
            : null;

    /// <summary>Resolves an override index to its component, or null when out of range (e.g. the manifest itself).</summary>
    public ModuleRef? Resolve(int moduleIndex)
    {
        var i = moduleIndex - _offset;
        if (i < 0 || i >= _components.Count)
            return null;
        var component = _components[i];
        return new ModuleRef(component.AssemblyName, component.Mvid, _providerFor(component.Mvid));
    }

    /// <summary>Resolves an override index directly to component metadata, or null when unavailable.</summary>
    public MetadataReader? ResolveMetadata(int moduleIndex) =>
        Resolve(moduleIndex)?.Provider?.GetMetadataReader();

    /// <summary>
    /// Resolves the system module used by composite owner-type signatures whose primitive type has
    /// no explicit module override.
    /// </summary>
    public MetadataReader? ResolveSystemMetadata()
    {
        if (!_systemMetadataProbed)
        {
            _systemMetadata = _systemModuleMvid is { } mvid
                ? _providerFor(mvid)?.GetMetadataReader()
                : null;
            _systemMetadataProbed = true;
        }

        return _systemMetadata;
    }

    // Component assembly indices start at two from R2R major 6.3 (readytorun.h version history); the
    // manifest reserves index 1 for itself, so a component is index (position + offset).
    private static int IndexOffset(ReadyToRunInfo info) =>
        info.MajorVersion > 6 || (info.MajorVersion == 6 && info.MinorVersion >= 3) ? 2 : 1;
}
