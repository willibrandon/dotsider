namespace Dotsider.Infrastructure;

/// <summary>
/// A discovered running dotsider instance.
/// </summary>
internal sealed record DiscoveredSession(int Pid, string SocketPath);
