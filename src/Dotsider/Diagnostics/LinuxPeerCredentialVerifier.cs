using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Dotsider.Diagnostics;

/// <summary>
/// Verifies peer credentials on Linux using <c>SO_PEERCRED</c>.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed partial class LinuxPeerCredentialVerifier : IPeerCredentialVerifier
{
    private const int SOL_SOCKET = 1;
    private const int SO_PEERCRED = 17;

    /// <summary>
    /// Returns <see langword="true"/> if the peer effective UID matches the current
    /// process effective UID.
    /// </summary>
    public bool IsSameUser(Socket client)
    {
        var cred = new UCred();
        var len = Marshal.SizeOf<UCred>();
        var result = getsockopt(
            (int)client.Handle, SOL_SOCKET, SO_PEERCRED,
            ref cred, ref len);

        if (result != 0)
            return false;

        return cred.Uid == geteuid();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UCred
    {
        public int Pid;
        public uint Uid;
        public uint Gid;
    }

    [LibraryImport("libc", SetLastError = true)]
    private static partial int getsockopt(
        int sockfd, int level, int optname,
        ref UCred optval, ref int optlen);

    [LibraryImport("libc")]
    private static partial uint geteuid();
}
