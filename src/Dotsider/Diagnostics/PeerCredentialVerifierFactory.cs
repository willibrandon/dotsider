using System.Runtime.Versioning;

namespace Dotsider.Diagnostics;

/// <summary>
/// Creates the platform-appropriate <see cref="IPeerCredentialVerifier"/>.
/// </summary>
internal static class PeerCredentialVerifierFactory
{
    /// <summary>
    /// Returns a verifier for the current operating system.
    /// </summary>
    public static IPeerCredentialVerifier Create()
    {
        if (OperatingSystem.IsLinux())
            return new LinuxPeerCredentialVerifier();

        if (OperatingSystem.IsMacOS())
            return new MacOsPeerCredentialVerifier();

        if (OperatingSystem.IsWindows())
            return CreateWindowsVerifier();

        throw new PlatformNotSupportedException(
            "Peer credential verification is not supported on this platform.");
    }

    [SupportedOSPlatform("windows")]
    private static WindowsPeerCredentialVerifier CreateWindowsVerifier() =>
        new();
}
