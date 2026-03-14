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

    // ── Guard actions ────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "BLOCKED ip={Ip} reason={Reason} ua={UserAgent}")]
    public static partial void ConnectionBlocked(ILogger logger, string ip, string reason, string? userAgent);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "BANNED ip={Ip} duration={DurationMin:F0}min reason={Reason} count={Count} windowSec={WindowSec:F0}")]
    public static partial void IpBanned(ILogger logger, string ip, double durationMin,
        string reason, int count, double windowSec);

    [LoggerMessage(Level = LogLevel.Critical,
        Message = "CIRCUIT TRIPPED connections={Count} in {WindowSec:F0}s — demo disabled for {CooldownSec:F0}s")]
    public static partial void CircuitTripped(ILogger logger, int count, double windowSec, double cooldownSec);

    [LoggerMessage(Level = LogLevel.Information, Message = "Circuit breaker reset — demo re-enabled")]
    public static partial void CircuitReset(ILogger logger);
}
