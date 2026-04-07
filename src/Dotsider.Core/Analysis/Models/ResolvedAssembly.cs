namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The result of resolving an assembly reference — either a file on disk or bytes from a bundle.
/// </summary>
public abstract record ResolvedAssembly
{
    /// <summary>
    /// The assembly was found as a file on disk.
    /// </summary>
    /// <param name="Path">Full path to the assembly file.</param>
    public sealed record FromFile(string Path) : ResolvedAssembly;

    /// <summary>
    /// The assembly was found inside a single-file bundle.
    /// </summary>
    /// <param name="Bytes">The raw assembly bytes extracted from the bundle.</param>
    /// <param name="Name">The assembly file name (e.g. "System.Runtime.dll").</param>
    /// <param name="BundlePath">Full path to the bundle file that contains this assembly.</param>
    public sealed record FromBundle(byte[] Bytes, string Name, string BundlePath) : ResolvedAssembly;
}
