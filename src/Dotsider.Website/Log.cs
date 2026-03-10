namespace Dotsider.Website;

/// <summary>
/// High-performance log messages for the dotsider website.
/// </summary>
internal static partial class Log
{
    /// <summary>
    /// Logs that a new WebSocket session has started.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Session started ({Active}/{Max})")]
    public static partial void SessionStarted(ILogger logger, int active, int max);

    /// <summary>
    /// Logs that a WebSocket session has ended.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Session ended ({Active}/{Max})")]
    public static partial void SessionEnded(ILogger logger, int active, int max);

    /// <summary>
    /// Logs an error that occurred during a WebSocket session.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Session error")]
    public static partial void SessionError(ILogger logger, Exception ex);
}
