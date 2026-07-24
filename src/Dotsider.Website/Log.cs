namespace Dotsider.Website;

/// <summary>
/// High-performance log messages for the dotsider website.
/// </summary>
internal static partial class Log
{
    // ── Session lifecycle ────────────────────────────────────────────

    /// <summary>
    /// Logs that a demo session has started.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="active">The number of active sessions.</param>
    /// <param name="max">The global session limit.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Session started ({Active}/{Max})")]
    public static partial void SessionStarted(ILogger logger, int active, int max);

    /// <summary>
    /// Logs that a demo session has ended.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="active">The number of active sessions.</param>
    /// <param name="max">The global session limit.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Session ended ({Active}/{Max})")]
    public static partial void SessionEnded(ILogger logger, int active, int max);

    /// <summary>
    /// Logs an unexpected demo session failure.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="ex">The session exception.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Session error")]
    public static partial void SessionError(ILogger logger, Exception ex);

    // ── Audit trail ──────────────────────────────────────────────────

    /// <summary>
    /// Logs a demo session connection for the audit trail.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ip">The resolved client address.</param>
    /// <param name="userAgent">The client user agent.</param>
    [LoggerMessage(Level = LogLevel.Information,
        Message = "CONNECT session={SessionId} ip={Ip} ua={UserAgent}")]
    public static partial void AuditConnect(ILogger logger, string sessionId, string ip, string? userAgent);

    /// <summary>
    /// Logs a demo session disconnection for the audit trail.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ip">The resolved client address.</param>
    /// <param name="durationSec">The session duration in seconds.</param>
    [LoggerMessage(Level = LogLevel.Information,
        Message = "DISCONNECT session={SessionId} ip={Ip} duration={DurationSec:F1}s")]
    public static partial void AuditDisconnect(ILogger logger, string sessionId, string ip, double durationSec);
}
