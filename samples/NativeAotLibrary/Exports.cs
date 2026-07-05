using System.Runtime.InteropServices;

namespace NativeAotLibrary;

/// <summary>
/// A Native AOT shared-library fixture. On Windows the published output is a native
/// <c>NativeAotLibrary.dll</c> that shares its filename with the pre-ILC managed input —
/// the self-collision case the sidecar probe must reject; on Linux/macOS it exercises
/// the <c>.so</c>/<c>.dylib</c> stem handling.
/// </summary>
public static class Exports
{
    /// <summary>Adds two integers through the unmanaged export surface.</summary>
    /// <param name="a">First addend.</param>
    /// <param name="b">Second addend.</param>
    [UnmanagedCallersOnly(EntryPoint = "dotsider_add")]
    public static int Add(int a, int b) => a + b;
}
