namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A type recovered from a Native AOT binary's embedded NativeFormat metadata. ILC strips
/// ECMA-335 metadata, but the reflection and stack-trace metadata it keeps still names the
/// binary's own types and methods, so a stripped binary can describe itself.
/// </summary>
/// <param name="FullName">The namespace-qualified type name (nested types use <c>+</c>).</param>
/// <param name="MethodNames">The names of the type's methods.</param>
public sealed record RecoveredType(string FullName, IReadOnlyList<string> MethodNames);
