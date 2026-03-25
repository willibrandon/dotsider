using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Dotsider.Diagnostics;

/// <summary>
/// Verifies peer credentials on macOS using <c>getpeereid(3)</c>.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed partial class MacOsPeerCredentialVerifier : IPeerCredentialVerifier
{
    /// <summary>
    /// Returns <see langword="true"/> if the peer effective UID matches the current
    /// process effective UID.
    /// </summary>
    public bool IsSameUser(Socket client)
    {
        uint euid = 0;
        uint egid = 0;
        var result = getpeereid((int)client.Handle, ref euid, ref egid);

        if (result != 0)
            return false;

        return euid == geteuid();
    }

    [LibraryImport("libc", SetLastError = true)]
    private static partial int getpeereid(int fd, ref uint euid, ref uint egid);

    [LibraryImport("libc")]
    private static partial uint geteuid();
}
