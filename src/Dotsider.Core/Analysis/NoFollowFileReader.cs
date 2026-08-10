using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads a regular file through an atomically opened handle that never follows a symbolic link or
/// Windows reparse point.
/// </summary>
internal static partial class NoFollowFileReader
{
    private const uint FileAttributeReparsePoint = 0x0000_0400;
    private const uint FileFlagOpenReparsePoint = 0x0020_0000;
    private const uint FileFlagSequentialScan = 0x0800_0000;
    private const uint FileShareRead = 0x0000_0001;
    private const uint GenericRead = 0x8000_0000;
    private const uint OpenExisting = 3;
    private const int FileAttributeTagInfo = 9;
    private const int LinuxOpenCloseOnExec = 0x0008_0000;
    private const int LinuxOpenNoFollowArm = 0x0000_8000;
    private const int LinuxOpenNoFollowGeneric = 0x0002_0000;
    private const int LinuxOpenNonBlocking = 0x0000_0800;

    /// <summary>
    /// Reads all bytes from <paramref name="path"/> without following a link at the final path
    /// component.
    /// </summary>
    /// <param name="path">The file path to open.</param>
    /// <param name="bytes">Receives the bytes read from the opened handle.</param>
    /// <returns><see langword="true"/> when a stable regular-file snapshot was read.</returns>
    internal static bool TryReadAllBytes(string path, out byte[] bytes)
    {
        bytes = [];
        try
        {
            using var handle = OpenWithoutFollowing(path);
            if (handle is null || handle.IsInvalid)
            {
                return false;
            }

            using var stream = new FileStream(handle, FileAccess.Read, bufferSize: 4096, isAsync: false);
            var length = stream.Length;
            if (length < 0 || length > int.MaxValue)
            {
                return false;
            }

            var snapshot = GC.AllocateUninitializedArray<byte>((int)length);
            stream.ReadExactly(snapshot);
            if (stream.ReadByte() != -1)
            {
                return false;
            }

            bytes = snapshot;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static SafeFileHandle? OpenWithoutFollowing(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            var handle = CreateFile(
                path,
                GenericRead,
                FileShareRead,
                IntPtr.Zero,
                OpenExisting,
                FileFlagOpenReparsePoint | FileFlagSequentialScan,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                return null;
            }

            if (!GetFileInformationByHandleEx(
                    handle,
                    FileAttributeTagInfo,
                    out var attributeTagInfo,
                    sizeof(long)) ||
                ((uint)attributeTagInfo & FileAttributeReparsePoint) != 0)
            {
                handle.Dispose();
                return null;
            }

            return handle;
        }

        int flags;
        if (OperatingSystem.IsLinux())
        {
            if (!TryGetLinuxOpenFlags(RuntimeInformation.ProcessArchitecture, out flags))
            {
                return null;
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            const int openCloseOnExec = 0x0100_0000;
            const int openNoFollow = 0x0000_0100;
            const int openNonBlocking = 0x0000_0004;
            flags = openCloseOnExec | openNoFollow | openNonBlocking;
        }
        else
        {
            return null;
        }

        var descriptor = Open(path, flags);
        return descriptor < 0
            ? null
            : new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
    }

    /// <summary>
    /// Gets Linux open flags for an architecture whose ABI values are known.
    /// Unknown architectures fail closed instead of opening a path without link protection.
    /// </summary>
    internal static bool TryGetLinuxOpenFlags(Architecture architecture, out int flags)
    {
        int openNoFollow = architecture switch
        {
            Architecture.Arm or
            Architecture.Arm64 or
            Architecture.Armv6 or
            Architecture.Ppc64le => LinuxOpenNoFollowArm,
            Architecture.LoongArch64 or
            Architecture.RiscV64 or
            Architecture.S390x or
            Architecture.X64 or
            Architecture.X86 => LinuxOpenNoFollowGeneric,
            _ => 0,
        };

        flags = openNoFollow == 0
            ? 0
            : LinuxOpenCloseOnExec | openNoFollow | LinuxOpenNonBlocking;
        return flags != 0;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        out long fileInformation,
        int bufferSize);

    [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Open(string path, int flags);
}
