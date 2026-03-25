using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Dotsider.Diagnostics;

/// <summary>
/// Creates and secures the dotsider socket directory and socket files.
/// </summary>
internal static class SocketDirectoryHelper
{
    /// <summary>
    /// Returns the socket directory path and ensures it exists with appropriate permissions.
    /// Permissions are set idempotently on every call to repair weak permissions from upgrades.
    /// On Linux/macOS: mode 0700. On Windows: ACL restricted to current user.
    /// </summary>
    public static string EnsureSocketDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotsider", "sockets");

        Directory.CreateDirectory(dir);

        if (OperatingSystem.IsWindows())
        {
            SecureWindowsDirectory(dir);
        }
        else
        {
            File.SetUnixFileMode(dir,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return dir;
    }

    /// <summary>
    /// Secures a socket file's ACL on Windows after <c>Bind()</c> creates it.
    /// Ensures the socket file itself is restricted to the current user, not just
    /// the parent directory.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void SecureSocketFile(string socketPath)
    {
        var fileInfo = new FileInfo(socketPath);
        var security = fileInfo.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        var currentUser = WindowsIdentity.GetCurrent();
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser.Name,
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        fileInfo.SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static void SecureWindowsDirectory(string dir)
    {
        var dirInfo = new DirectoryInfo(dir);
        var security = dirInfo.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        var currentUser = WindowsIdentity.GetCurrent();
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser.Name,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        dirInfo.SetAccessControl(security);
    }
}
