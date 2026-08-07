namespace Dotsider.TraceHost;

/// <summary>
/// Monitors commands sent by the Native AOT parent process.
/// Ignores unknown commands without abandoning graceful shutdown.
/// Stops the traced process when requested or when the parent disconnects.
/// </summary>
internal static class TraceHostControlChannel
{
    internal static async Task MonitorAsync(TextReader reader, Action stop)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(stop);

        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } command)
            {
                if (!command.Equals("stop", StringComparison.Ordinal))
                    continue;

                stop();
                return;
            }
        }
        catch (IOException)
        {
        }

        stop();
    }
}
