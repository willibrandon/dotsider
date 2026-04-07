using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Detects .NET apphost executables and locates their companion managed assemblies.
/// </summary>
/// <remarks>
/// <c>dotnet build</c> produces both a managed <c>.dll</c> (the actual assembly with IL and
/// metadata) and a native apphost launcher that bootstraps the runtime. On Windows the
/// apphost is a <c>.exe</c> (PE format); on Linux and macOS it is an extensionless
/// executable (ELF or Mach-O). The apphost has no CLR metadata, so most analysis tabs
/// are empty. This detector verifies the file is an apphost by requiring two signals:
/// the companion DLL name embedded in the binary AND a reference to <c>hostfxr</c>
/// (the .NET host framework resolver). These signals are platform-invariant — the
/// .NET SDK embeds them identically regardless of binary format.
/// </remarks>
public static class ApphostDetector
{
    /// <summary>
    /// If the binary at <paramref name="exePath"/> is a .NET apphost (embeds both the
    /// companion DLL name and a <c>hostfxr</c> reference), returns the path to the
    /// companion <c>.dll</c> provided it has readable .NET metadata. Works with Windows
    /// <c>.exe</c> files and extensionless Linux/macOS executables.
    /// </summary>
    /// <param name="exePath">Path to the executable file.</param>
    /// <returns>
    /// The full path to the companion managed <c>.dll</c>, or <c>null</c> if the file
    /// is not an apphost, no companion exists, or the companion has no readable .NET
    /// metadata.
    /// </returns>
    public static string? FindCompanionDll(string exePath)
    {
        // A .dll is already a managed assembly — never redirect it.
        if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return null;

        // For .exe files, strip the extension (Foo.exe → Foo.dll).
        // For extensionless files (Linux/macOS apphosts), append .dll to the
        // full filename. GetFileNameWithoutExtension can't be used because it
        // treats dots in the assembly name as extensions
        // (e.g., Company.Product.Tool → Company.Product instead of Company.Product.Tool).
        var fileName = Path.GetFileName(exePath);
        var dllName = exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName) + ".dll"
            : fileName + ".dll";
        var dllPath = Path.Combine(Path.GetDirectoryName(exePath)!, dllName);
        if (!File.Exists(dllPath))
            return null;

        // Verify the file is actually a .NET apphost by requiring two signals:
        // 1. The companion DLL name is embedded (apphost bakes it as a UTF-8 string)
        // 2. A reference to "hostfxr" exists (the .NET host framework resolver that
        //    every apphost imports to bootstrap the runtime)
        // Either alone is a weak heuristic; together they rule out unrelated native
        // executables that happen to reference the same DLL name. These signals are
        // embedded identically in PE (.exe), ELF, and Mach-O apphost binaries.
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

    /// <summary>
    /// If the file at <paramref name="exePath"/> is a single-file bundle, extracts the
    /// entry assembly (the app's own managed code) and returns its bytes and name.
    /// Uses dotted-name-safe basename matching to locate the entry assembly within
    /// the bundle manifest.
    /// </summary>
    /// <param name="exePath">Path to the executable file.</param>
    /// <returns>
    /// The entry assembly bytes and file name, or <c>null</c> if the file is not a
    /// single-file bundle or the entry assembly could not be identified.
    /// </returns>
    public static (byte[] Bytes, string Name)? FindBundledEntryAssembly(string exePath)
    {
        // A .dll is already a managed assembly — never probe it as a bundle.
        if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return null;

        return SingleFileBundleReader.FindEntryAssembly(exePath);
    }

    private static bool ContainsSequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
            return false;

        return haystack.IndexOf(needle) >= 0;
    }
}
