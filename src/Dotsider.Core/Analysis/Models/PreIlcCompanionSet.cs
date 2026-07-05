namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The attached pre-ILC companions of a Native AOT binary: the root managed input and any
/// validated local reference assemblies.
/// </summary>
/// <remarks>
/// <b>Ownership:</b> the set and every analyzer in it are owned by the
/// <see cref="AssemblyAnalyzer"/> they were attached to. Consumers must never dispose
/// <see cref="Root"/> or <see cref="LocalReferences"/> — they become invalid when the
/// owner detaches or is disposed. The type deliberately does not implement
/// <see cref="System.IDisposable"/>; teardown is internal to the owning analyzer.
/// </remarks>
public sealed class PreIlcCompanionSet
{
    internal PreIlcCompanionSet(AssemblyAnalyzer root, IReadOnlyList<AssemblyAnalyzer> localReferences)
    {
        Root = root;
        LocalReferences = localReferences;
        var all = new List<AssemblyAnalyzer>(1 + localReferences.Count) { root };
        all.AddRange(localReferences);
        All = all;
    }

    /// <summary>The root managed input — the assembly ILC compiled. Metadata surfaces route here first.</summary>
    public AssemblyAnalyzer Root { get; }

    /// <summary>Local/project reference assemblies that also fed the compilation, validated on attach.</summary>
    public IReadOnlyList<AssemblyAnalyzer> LocalReferences { get; }

    /// <summary>The root followed by the local references.</summary>
    public IReadOnlyList<AssemblyAnalyzer> All { get; }

    /// <summary>Finds a member of the set by assembly simple name, or null.</summary>
    /// <param name="name">The assembly simple name to look for.</param>
    public AssemblyAnalyzer? FindByAssemblyName(string name)
    {
        foreach (var analyzer in All)
        {
            if (string.Equals(analyzer.AssemblyName, name, StringComparison.Ordinal))
                return analyzer;
        }

        return null;
    }

    /// <summary>Disposes every member. Called only by the owning <see cref="AssemblyAnalyzer"/>.</summary>
    internal void Dispose()
    {
        foreach (var analyzer in All)
            analyzer.Dispose();
    }
}
