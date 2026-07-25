namespace Dotsider.Website;

internal sealed class DemoOptions
{
    internal const string SectionName = "Demo";

    /// <summary>
    /// Gets or sets the origins allowed to establish WebSocket connections.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum number of concurrent demo sessions.
    /// </summary>
    public int MaxSessions { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of concurrent demo sessions per client address.
    /// </summary>
    public int MaxSessionsPerClient { get; set; } = 3;

    /// <summary>
    /// Gets or sets the sample assembly opened by the demo.
    /// </summary>
    public string SampleAssembly { get; set; } = "sample.dll";

    /// <summary>
    /// Gets or sets the maximum session duration in minutes.
    /// </summary>
    public int SessionTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the exact proxy addresses trusted to supply <c>X-Forwarded-For</c>.
    /// </summary>
    public string[] TrustedProxies { get; set; } = [];
}
