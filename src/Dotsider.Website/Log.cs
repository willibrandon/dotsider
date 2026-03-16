using System.Net;

namespace Dotsider.Website;

/// <summary>
/// High-performance log messages for the dotsider website.
/// </summary>
internal static partial class Log
{
    // ── Session lifecycle ────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information, Message = "Session started ({Active}/{Max})")]
    public static partial void SessionStarted(ILogger logger, int active, int max);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session ended ({Active}/{Max})")]
    public static partial void SessionEnded(ILogger logger, int active, int max);

    [LoggerMessage(Level = LogLevel.Error, Message = "Session error")]
    public static partial void SessionError(ILogger logger, Exception ex);

    // ── Audit trail ──────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "CONNECT session={SessionId} ip={Ip} ua={UserAgent}")]
    public static partial void AuditConnect(ILogger logger, string sessionId, IPAddress ip, string? userAgent);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "DISCONNECT session={SessionId} ip={Ip} duration={DurationSec:F1}s")]
    public static partial void AuditDisconnect(ILogger logger, string sessionId, IPAddress ip, double durationSec);
}
