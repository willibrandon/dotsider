using System.Runtime.InteropServices;

namespace NativeLib;

/// <summary>
/// P/Invoke declarations for native interop testing.
/// These don't need to actually resolve — dotsider only inspects metadata.
/// </summary>
internal static partial class NativeInterop
{
    [LibraryImport("kernel32", EntryPoint = "GetCurrentProcessId")]
    internal static partial uint GetCurrentProcessId();

    [LibraryImport("libc", EntryPoint = "getpid")]
    internal static partial int GetPid();

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int MessageBox(nint hWnd, string text, string caption, uint type);
}
