namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Represents the resolved target of an IL code navigation (go-to-definition) action.
/// </summary>
public abstract record IlNavigationTarget
{
    /// <summary>A method defined in the current assembly.</summary>
    public sealed record LocalMethod(MethodDefInfo Method) : IlNavigationTarget;

    /// <summary>A type defined in the current assembly.</summary>
    public sealed record LocalType(TypeDefInfo Type) : IlNavigationTarget;

    /// <summary>A field defined in the current assembly.</summary>
    public sealed record LocalField(FieldDefInfo Field, TypeDefInfo DeclaringType) : IlNavigationTarget;

    /// <summary>A method in an external (referenced) assembly.</summary>
    public sealed record ExternalMethod(
        string MemberName, string DeclaringType, string Signature, string AssemblyName) : IlNavigationTarget;

    /// <summary>A type in an external (referenced) assembly.</summary>
    public sealed record ExternalType(TypeRefInfo TypeRef, string AssemblyName) : IlNavigationTarget;

    /// <summary>A field in an external (referenced) assembly.</summary>
    public sealed record ExternalField(
        string FieldName, string DeclaringType, string AssemblyName) : IlNavigationTarget;

    /// <summary>A MethodSpec whose metadata could not be decoded into a navigable target.</summary>
    public sealed record GenericInstantiation(int Token, string Reason) : IlNavigationTarget;

    /// <summary>A token kind that is recognized but not supported for navigation.</summary>
    public sealed record Unsupported(int Token, string Reason) : IlNavigationTarget;

    /// <summary>A token that could not be resolved to any known target.</summary>
    public sealed record Unresolved(int Token, string Reason) : IlNavigationTarget;
}
