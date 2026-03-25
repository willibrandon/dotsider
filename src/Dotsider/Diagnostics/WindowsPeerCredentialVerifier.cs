using System.Net.Sockets;
using System.Runtime.Versioning;

namespace Dotsider.Diagnostics;

/// <summary>
/// Verifies peer credentials on Windows.
/// Windows UDS relies on directory and socket file ACLs (set by
/// <see cref="SocketDirectoryHelper"/>) for access control. Any connection
/// that reaches the socket has already passed the OS-level ACL check,
/// so this verifier returns <see langword="true"/> unconditionally.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsPeerCredentialVerifier : IPeerCredentialVerifier
{
    /// <inheritdoc/>
    public bool IsSameUser(Socket client) => true;
}
