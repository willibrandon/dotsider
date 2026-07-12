using System.Reflection.Metadata;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Contains the rows and validated names produced by a bounded metadata nesting-chain walk.
/// </summary>
/// <typeparam name="THandle">The strongly typed metadata handle stored in the chain.</typeparam>
internal readonly struct ChainWalkResult<THandle>
    where THandle : struct
{
    /// <summary>
    /// Initializes a metadata nesting-chain result.
    /// </summary>
    /// <param name="first">The first row in the chain.</param>
    /// <param name="firstNamespace">The validated namespace decoded from the first row.</param>
    /// <param name="firstName">The validated name decoded from the first row.</param>
    /// <param name="rest">Rows after <paramref name="first"/>, in traversal order.</param>
    /// <param name="restNames">Validated names for <paramref name="rest"/>, in the same order.</param>
    /// <param name="outermostNamespace">The validated namespace decoded from the outermost row.</param>
    /// <param name="terminal">The terminal relationship handle, when one exists.</param>
    /// <param name="termination">The reason the walk ended.</param>
    internal ChainWalkResult(
        THandle first,
        string firstNamespace,
        string firstName,
        IReadOnlyList<THandle>? rest,
        IReadOnlyList<string>? restNames,
        string outermostNamespace,
        EntityHandle terminal,
        ChainTermination termination)
    {
        First = first;
        FirstNamespace = firstNamespace;
        FirstName = firstName;
        Rest = rest;
        RestNames = restNames;
        OutermostNamespace = outermostNamespace;
        Terminal = terminal;
        Termination = termination;
    }

    /// <summary>Gets the first row in the chain.</summary>
    public THandle First { get; }

    /// <summary>Gets the validated namespace decoded from <see cref="First"/>.</summary>
    public string FirstNamespace { get; }

    /// <summary>Gets the validated name decoded from <see cref="First"/>.</summary>
    public string FirstName { get; }

    /// <summary>Gets rows after <see cref="First"/>, in traversal order.</summary>
    public IReadOnlyList<THandle>? Rest { get; }

    /// <summary>Gets validated names for <see cref="Rest"/>, in the same traversal order.</summary>
    public IReadOnlyList<string>? RestNames { get; }

    /// <summary>Gets the validated namespace decoded from the outermost valid row.</summary>
    public string OutermostNamespace { get; }

    /// <summary>Gets the legal terminal relationship handle, when one exists.</summary>
    public EntityHandle Terminal { get; }

    /// <summary>Gets the reason the walk ended.</summary>
    public ChainTermination Termination { get; }

    /// <summary>Gets whether the walk reached a legal terminal.</summary>
    public bool IsComplete => Termination == ChainTermination.Complete;
}
