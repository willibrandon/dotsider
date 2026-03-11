namespace Dotsider.Website;

/// <summary>
/// Configuration for the demo guard.
/// </summary>
internal sealed class DemoGuardOptions
{
    /// <summary>Max new connections per IP within the rate window.</summary>
    public int MaxConnectionsPerIpPerWindow { get; set; } = 10;

    /// <summary>Time window for per-IP rate limiting.</summary>
    public TimeSpan RateWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Max concurrent WebSocket sessions per IP.</summary>
    public int MaxConcurrentPerIp { get; set; } = 3;

    /// <summary>Base ban duration for the first offense. Escalates on repeat violations.</summary>
    public TimeSpan BanDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Maximum ban duration regardless of escalation. Never exceeds 24 hours.</summary>
    public TimeSpan MaxBanDuration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Global connection count that trips the circuit breaker.</summary>
    public int CircuitThreshold { get; set; } = 50;

    /// <summary>Time window for the global circuit breaker.</summary>
    public TimeSpan CircuitWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>How long the circuit stays open before auto-resetting.</summary>
    public TimeSpan CircuitCooldown { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Sessions shorter than this count as suspicious rapid disconnects.</summary>
    public TimeSpan SuspiciousSessionDuration { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Number of rapid disconnects before auto-ban.</summary>
    public int MaxRapidDisconnects { get; set; } = 5;
}
