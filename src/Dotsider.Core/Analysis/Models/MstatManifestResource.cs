namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One embedded manifest resource from an ILC size report (format 2.1+). For back-compat
/// these bytes are also summed into the <c>ResourceData</c> blob entry.
/// </summary>
/// <param name="AssemblyName">The simple name of the assembly the resource was embedded in.</param>
/// <param name="Name">The resource name.</param>
/// <param name="Size">The resource size in bytes.</param>
public sealed record MstatManifestResource(
    string AssemblyName,
    string Name,
    int Size);
