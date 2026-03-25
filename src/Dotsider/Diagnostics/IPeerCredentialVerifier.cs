using System.Net.Sockets;

namespace Dotsider.Diagnostics;

/// <summary>
/// Verifies that a connected Unix domain socket peer belongs to the same user.
/// </summary>
internal interface IPeerCredentialVerifier
{
    /// <summary>
    /// Returns <see langword="true"/> if the connected peer belongs to the same
    /// user as the current process; <see langword="false"/> otherwise.
    /// </summary>
    bool IsSameUser(Socket client);
}
