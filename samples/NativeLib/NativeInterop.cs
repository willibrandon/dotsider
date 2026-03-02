using System.Runtime.InteropServices;

namespace NativeLib;

/// <summary>
/// P/Invoke declarations for native interop testing.
/// These don't need to actually resolve — dotsider only inspects metadata.
/// </summary>
public static partial class NativeInterop
{
    [LibraryImport("kernel32", EntryPoint = "GetCurrentProcessId")]
    public static partial uint GetCurrentProcessId();

    [DllImport("libc", EntryPoint = "getpid")]
    public static extern int GetPid();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int MessageBox(nint hWnd, string text, string caption, uint type);
}
