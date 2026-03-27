using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Detects .NET apphost executables and locates their companion managed assemblies.
/// </summary>
/// <remarks>
/// <c>dotnet build</c> produces both a managed <c>.dll</c> (the actual assembly with IL and
/// metadata) and a native apphost <c>.exe</c> (a launcher that bootstraps the runtime).
/// The apphost has no CLR metadata, so most analysis tabs are empty. This detector
/// verifies the <c>.exe</c> is an apphost by requiring two signals: the companion DLL
/// name embedded in the binary AND a reference to <c>hostfxr</c> (the .NET host
/// framework resolver that every apphost imports to bootstrap the runtime).
/// </remarks>
public static class ApphostDetector
{
    /// <summary>
    /// If <paramref name="exePath"/> ends with <c>.exe</c> and the binary is a .NET
    /// apphost (embeds both the companion DLL name and a <c>hostfxr</c> reference),
    /// returns the path to the companion <c>.dll</c> provided it has readable .NET metadata.
    /// </summary>
    /// <param name="exePath">Path to the executable file.</param>
    /// <returns>
    /// The full path to the companion managed <c>.dll</c>, or <c>null</c> if the file
    /// is not an apphost, no companion exists, or the companion has no readable .NET
    /// metadata.
    /// </returns>
    public static string? FindCompanionDll(string exePath)
    {
        if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return null;

        var dllName = Path.GetFileNameWithoutExtension(exePath) + ".dll";
        var dllPath = Path.Combine(Path.GetDirectoryName(exePath)!, dllName);
        if (!File.Exists(dllPath))
            return null;

        // Verify the .exe is actually a .NET apphost by requiring two signals:
        // 1. The companion DLL name is embedded (apphost bakes it as a UTF-8 string)
        // 2. A reference to "hostfxr" exists (the .NET host framework resolver that
        //    every apphost imports to bootstrap the runtime)
        // Either alone is a weak heuristic; together they rule out unrelated native
        // executables that happen to reference the same DLL name.
        try
        {
            var exeBytes = File.ReadAllBytes(exePath);
            if (!ContainsSequence(exeBytes, Encoding.UTF8.GetBytes(dllName))
                || !ContainsSequence(exeBytes, "hostfxr"u8))
                return null;
        }
        catch
        {
            return null;
        }

        // Verify the companion .dll has readable .NET metadata
        try
        {
            using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                return null;

            _ = peReader.GetMetadataReader();
            return dllPath;
        }
        catch
        {
            return null;
        }
    }

    private static bool ContainsSequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
            return false;

        return haystack.IndexOf(needle) >= 0;
    }
}
