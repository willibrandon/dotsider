namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A managed method joined to its precompiled ReadyToRun native code: the owning assembly
/// identity, the MethodDef token (or the instantiation for a generic), and the full ordered list
/// of <see cref="ReadyToRunCodeRange"/> blocks that make up the body.
/// </summary>
/// <param name="AssemblyName">The simple name of the assembly that owns the method.</param>
/// <param name="Mvid">The owning assembly's module version id (composite identity validation).</param>
/// <param name="Token">The method's metadata token (<c>0x06000000 | rid</c>).</param>
/// <param name="DeclaringType">The declaring type's display name, or null when metadata is unavailable.</param>
/// <param name="Name">The method's simple name, or null when metadata is unavailable.</param>
/// <param name="Signature">The method's decoded signature, or null when metadata is unavailable.</param>
/// <param name="CodeRanges">The ordered native code blocks (hot entry, funclets, cold) — never empty for a precompiled method.</param>
/// <param name="EntryPointRuntimeFunctionId">The index of the method's first runtime function in the RuntimeFunctions table.</param>
/// <param name="RuntimeFunctionCount">The number of runtime functions the method owns (hot funclets plus cold).</param>
/// <param name="IsGenericInstantiation">Whether this entry is a generic instantiation from the InstanceMethodEntryPoints table.</param>
/// <param name="InstantiationDisplay">A rendered instantiation (e.g. <c>Describe&lt;int&gt;</c>), or null for a non-generic entry.</param>
/// <param name="TotalSize">The total native code size in bytes, summed across <paramref name="CodeRanges"/>.</param>
public sealed record ReadyToRunMethodEntry(
    string AssemblyName,
    Guid Mvid,
    int Token,
    string? DeclaringType,
    string? Name,
    string? Signature,
    IReadOnlyList<ReadyToRunCodeRange> CodeRanges,
    int EntryPointRuntimeFunctionId,
    int RuntimeFunctionCount,
    bool IsGenericInstantiation,
    string? InstantiationDisplay,
    long TotalSize);
