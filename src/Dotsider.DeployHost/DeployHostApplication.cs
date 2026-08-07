namespace Dotsider.DeployHost;

/// <summary>
/// Dispatches the fixed deployment host command set.
/// Each command uses literal process arguments and fixed privileged paths.
/// Unexpected failures are reported without exposing command input or secrets.
/// </summary>
internal static class DeployHostApplication
{
    /// <summary>
    /// Runs one deployment host command and returns its process exit code.
    /// Commands that change the host require Linux and effective root access.
    /// Cancellation uses the conventional exit code 130.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="cancellationToken">Stops the requested operation.</param>
    /// <returns>The command exit code.</returns>
    internal static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length != 1)
        {
            WriteUsage(Console.Error);
            return 2;
        }

        using var httpClient = new HttpClient();
        var processRunner = new ProcessRunner();
        try
        {
            return args[0] switch
            {
                "provision" => await RunPrivilegedAsync(
                    token => new ProvisionCommand(processRunner, httpClient, Console.Out).RunAsync(token),
                    cancellationToken).ConfigureAwait(false),
                "activate" => await RunPrivilegedAsync(
                    token => new ActivateCommand(processRunner, httpClient, Console.Out).RunAsync(token),
                    cancellationToken).ConfigureAwait(false),
                "preflight" => await new PreflightCommand(processRunner, httpClient, Console.Out)
                    .RunAsync(cancellationToken).ConfigureAwait(false),
                "report" => await new MetricsReportCommand(
                    httpClient,
                    TimeProvider.System,
                    DeployPaths.PrometheusUrl,
                    DeployPaths.MetricsLogPath).RunAsync(cancellationToken).ConfigureAwait(false),
                "integrity" => await new IntegrityCommand(
                    processRunner,
                    TimeProvider.System,
                    DeployPaths.SampleDirectory,
                    DeployPaths.SampleBackupDirectory,
                    DeployPaths.SampleManifestPath,
                    DeployPaths.IntegrityLogPath).RunAsync(cancellationToken).ConfigureAwait(false),
                _ => InvalidCommand(),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Deployment failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunPrivilegedAsync(
        Func<CancellationToken, Task<int>> operation,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("This command requires Linux.");
            return 1;
        }

        if (Environment.UserName != "root")
        {
            Console.Error.WriteLine("This command must run as root.");
            return 1;
        }

        return await operation(cancellationToken).ConfigureAwait(false);
    }

    private static int InvalidCommand()
    {
        WriteUsage(Console.Error);
        return 2;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: dotsider-deploy-host <provision|preflight|activate|report|integrity>");
    }
}
