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

    [DllImport("libc", EntryPoint = "getpid")]
    internal static extern int GetPid();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int MessageBox(nint hWnd, string text, string caption, uint type);
}
