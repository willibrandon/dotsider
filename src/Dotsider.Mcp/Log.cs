using Microsoft.Extensions.Logging;

namespace Dotsider.Mcp;

/// <summary>
/// High-performance log messages for the MCP server CallTool filter.
/// Uses the <see cref="LoggerMessageAttribute"/> source generator to avoid
/// unnecessary argument evaluation when the log level is disabled.
/// </summary>
internal static partial class Log
{
    /// <summary>
    /// Logs that a tool invocation has started.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="toolName">The name of the tool being invoked.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Invoking tool {ToolName}")]
    public static partial void ToolInvoking(ILogger logger, string toolName);

    /// <summary>
    /// Logs that a tool invocation completed successfully.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="toolName">The name of the tool that completed.</param>
    /// <param name="elapsedMs">The elapsed time in milliseconds.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Tool {ToolName} completed in {ElapsedMs}ms")]
    public static partial void ToolCompleted(ILogger logger, string toolName, long elapsedMs);

    /// <summary>
    /// Logs that a tool returned an error result.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="toolName">The name of the tool that returned an error.</param>
    /// <param name="elapsedMs">The elapsed time in milliseconds.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool {ToolName} returned error after {ElapsedMs}ms")]
    public static partial void ToolReturnedError(ILogger logger, string toolName, long elapsedMs);

    /// <summary>
    /// Logs that a tool threw an unhandled exception.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="toolName">The name of the tool that threw.</param>
    /// <param name="elapsedMs">The elapsed time in milliseconds.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Tool {ToolName} threw unhandled exception after {ElapsedMs}ms")]
    public static partial void ToolUnhandledException(ILogger logger, Exception exception, string toolName, long elapsedMs);
}
