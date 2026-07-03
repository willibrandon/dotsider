namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A type recovered from a Native AOT binary's embedded NativeFormat metadata. ILC strips
/// ECMA-335 metadata, but the reflection and stack-trace metadata it keeps still names the
/// binary's own types and methods, so a stripped binary can describe itself.
/// </summary>
/// <param name="FullName">The namespace-qualified type name (nested types use <c>+</c>).</param>
/// <param name="MethodNames">The names of the type's methods, in metadata order.</param>
/// <param name="AssemblyName">
/// The simple name of the assembly scope that defined the type, or null when the metadata
/// does not record one. Native symbol demangling joins mangled names against this scope.
/// </param>
public sealed record RecoveredType(
    string FullName,
    IReadOnlyList<string> MethodNames,
    string? AssemblyName = null)
{
    /// <summary>
    /// Deconstructs into the original two components, preserving call sites written before
    /// <see cref="AssemblyName"/> existed — a record's generated Deconstruct grows with its
    /// positional parameters, so the two-value form is kept explicitly.
    /// </summary>
    /// <param name="fullName">The namespace-qualified type name.</param>
    /// <param name="methodNames">The names of the type's methods, in metadata order.</param>
    public void Deconstruct(out string fullName, out IReadOnlyList<string> methodNames)
    {
        fullName = FullName;
        methodNames = MethodNames;
    }
}
