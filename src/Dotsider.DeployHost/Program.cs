namespace Dotsider.DeployHost;

/// <summary>
/// Provides the native entry point for privileged deployment host operations.
/// The command dispatcher keeps systemd invocations small and deterministic.
/// Exit codes communicate success or failure to provisioning and deployment automation.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancellationSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationSource.Cancel();
        };

        return await DeployHostApplication.RunAsync(args, cancellationSource.Token).ConfigureAwait(false);
    }
}
