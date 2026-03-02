using System.Reflection;

namespace Dotsider.Analysis.Models;

/// <summary>
/// Information about a method defined in the assembly's MethodDef metadata table.
/// </summary>
/// <param name="Token">The metadata token for this method definition.</param>
/// <param name="DeclaringType">The fully qualified name of the type that declares this method.</param>
/// <param name="Name">The simple name of the method.</param>
/// <param name="Signature">The decoded method signature string (e.g., "void(int, string)").</param>
/// <param name="Attributes">The method attribute flags (access, vtable layout, implementation).</param>
/// <param name="ImplAttributes">The method implementation attribute flags (IL, native, runtime).</param>
/// <param name="Rva">The relative virtual address of the method body, or zero for abstract/extern methods.</param>
public sealed record MethodDefInfo(
    int Token,
    string DeclaringType,
    string Name,
    string Signature,
    MethodAttributes Attributes,
    MethodImplAttributes ImplAttributes,
    int Rva);
