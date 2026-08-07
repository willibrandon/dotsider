/// <summary>
/// Packages the Linux deployment host and orchestrates preflight or production deployment.
/// Remote transfer retains the established rsync paths, deletion rules, and exclusions.
/// Failed deployments restore an integrity timer that was active before deployment began.
/// </summary>
internal static class DeploymentApp
{
    private const int MaxOutputCharacters = 256 * 1024;

    /// <summary>
    /// Runs the requested deployment utility mode.
    /// Errors produce a concise diagnostic and a nonzero exit code.
    /// Secret material is never included in process output or exception text.
    /// </summary>
    /// <param name="args">The utility command-line arguments.</param>
    /// <returns>Zero on success; otherwise one.</returns>
    internal static int Run(string[] args)
    {
        return Run(args, new DeploymentProcessRunner());
    }

    /// <summary>
    /// Runs the requested deployment utility mode with a supplied process runner.
    /// Tests use this boundary to verify remote failure recovery deterministically.
    /// Production uses the shell-free shared script process implementation.
    /// </summary>
    /// <param name="args">The utility command-line arguments.</param>
    /// <param name="processRunner">The local process runner.</param>
    /// <returns>Zero on success; otherwise one.</returns>
    internal static int Run(string[] args, IDeploymentProcessRunner processRunner)
    {
        try
        {
            DeploymentOptions options = DeploymentOptions.Parse(args);
            return options.Mode switch
            {
                "Package" => Package(options, processRunner),
                "Provision" => Provision(options, processRunner),
                "Preflight" => Preflight(options, processRunner),
                "Deploy" => Deploy(options, processRunner),
                _ => throw new InvalidOperationException("Unsupported deployment mode."),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Deployment failed: {exception.Message}");
            return 1;
        }
    }

    private static int Package(DeploymentOptions options, IDeploymentProcessRunner processRunner)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(options.DeployHostPath)!);
        string outputDirectory = Path.GetDirectoryName(options.DeployHostPath)!;
        RunRequired(
            processRunner,
            "dotnet",
            [
                "publish",
                Path.Combine(options.RepositoryRoot, "src", "Dotsider.DeployHost", "Dotsider.DeployHost.csproj"),
                "-c",
                "Release",
                "-r",
                options.Rid,
                "--self-contained",
                "-o",
                outputDirectory,
            ],
            options.RepositoryRoot);
        if (!File.Exists(options.DeployHostPath))
        {
            throw new FileNotFoundException("Native AOT deployment host was not produced.", options.DeployHostPath);
        }

        Console.WriteLine($"Packaged: {options.DeployHostPath}");
        return 0;
    }

    private static int Preflight(DeploymentOptions options, IDeploymentProcessRunner processRunner)
    {
        using var ssh = new SshContext(options.SshKey, options.Host, processRunner, options.RepositoryRoot);
        string remoteCandidate = CreateRemoteCandidatePath();
        try
        {
            UploadCandidate(options, ssh, remoteCandidate, processRunner);
            RunSshRequired(options, ssh, [remoteCandidate, "preflight"], processRunner);
            return 0;
        }
        finally
        {
            RemoveRemoteCandidate(options, ssh, remoteCandidate, processRunner);
        }
    }

    private static int Provision(DeploymentOptions options, IDeploymentProcessRunner processRunner)
    {
        using var ssh = new SshContext(options.SshKey, options.Host, processRunner, options.RepositoryRoot);
        string remoteCandidate = CreateRemoteCandidatePath();
        try
        {
            UploadCandidate(options, ssh, remoteCandidate, processRunner);
            RunSshRequired(options, ssh, [remoteCandidate, "provision"], processRunner);
            return 0;
        }
        finally
        {
            RemoveRemoteCandidate(options, ssh, remoteCandidate, processRunner);
        }
    }

    private static int Deploy(DeploymentOptions options, IDeploymentProcessRunner processRunner)
    {
        using var ssh = new SshContext(options.SshKey, options.Host, processRunner, options.RepositoryRoot);
        string remoteCandidate = CreateRemoteCandidatePath();
        bool timerWasActive = RunSsh(
            options,
            ssh,
            ["systemctl", "is-active", "--quiet", "integrity-check.timer"],
            processRunner).ExitCode == 0;
        var deploymentSucceeded = false;
        try
        {
            _ = RunSsh(
                options,
                ssh,
                ["sudo", "systemctl", "stop", "integrity-check.timer", "integrity-check.service"],
                processRunner);
            Rsync(options, ssh, options.DocsPath, "/var/www/dotsider-docs/", ["--delete"], processRunner);
            Rsync(
                options,
                ssh,
                options.WebsitePath,
                "/opt/dotsider-website/",
                [
                    "--delete",
                    "--chmod=F755",
                    "--exclude=sample/",
                    "--exclude=sample.bak/",
                    "--exclude=sample.sha256",
                ],
                processRunner);
            Rsync(
                options,
                ssh,
                options.SamplePath,
                "/opt/dotsider-website/sample/",
                ["--delete", "--chmod=F644"],
                processRunner);
            UploadCandidate(options, ssh, remoteCandidate, processRunner);
            RunSshRequired(options, ssh, ["sudo", remoteCandidate, "activate"], processRunner);
            deploymentSucceeded = true;
            return 0;
        }
        finally
        {
            RemoveRemoteCandidate(options, ssh, remoteCandidate, processRunner);
            if (!deploymentSucceeded && timerWasActive)
            {
                RunSshRequired(
                    options,
                    ssh,
                    ["sudo", "systemctl", "start", "integrity-check.timer"],
                    processRunner);
            }
        }
    }

    private static void Rsync(
        DeploymentOptions options,
        SshContext ssh,
        string sourceDirectory,
        string destinationDirectory,
        IReadOnlyList<string> additionalArguments,
        IDeploymentProcessRunner processRunner)
    {
        string source = Path.TrimEndingDirectorySeparator(sourceDirectory) + Path.DirectorySeparatorChar;
        string remoteShell = string.Join(' ', ssh.CreateArguments().Prepend("ssh").Select(QuoteRsyncShellArgument));
        var arguments = new List<string> { "-avz" };
        arguments.AddRange(additionalArguments);
        arguments.Add("-e");
        arguments.Add(remoteShell);
        arguments.Add(source);
        arguments.Add($"{options.User}@{options.Host}:{destinationDirectory}");
        RunRequired(processRunner, "rsync", arguments, options.RepositoryRoot);
    }

    private static void UploadCandidate(
        DeploymentOptions options,
        SshContext ssh,
        string remoteCandidate,
        IDeploymentProcessRunner processRunner)
    {
        var arguments = new List<string>(ssh.CreateArguments())
        {
            options.DeployHostPath,
            $"{options.User}@{options.Host}:{remoteCandidate}",
        };
        RunRequired(processRunner, "scp", arguments, options.RepositoryRoot);
        RunSshRequired(options, ssh, ["chmod", "0700", remoteCandidate], processRunner);
    }

    private static void RemoveRemoteCandidate(
        DeploymentOptions options,
        SshContext ssh,
        string remoteCandidate,
        IDeploymentProcessRunner processRunner)
    {
        _ = RunSsh(options, ssh, ["rm", "-f", "--", remoteCandidate], processRunner);
    }

    private static void RunSshRequired(
        DeploymentOptions options,
        SshContext ssh,
        IReadOnlyList<string> remoteArguments,
        IDeploymentProcessRunner processRunner)
    {
        DeploymentProcessResult result = RunSsh(options, ssh, remoteArguments, processRunner);
        Console.Write(result.StandardOutput);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Remote command failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
        }
    }

    private static DeploymentProcessResult RunSsh(
        DeploymentOptions options,
        SshContext ssh,
        IReadOnlyList<string> remoteArguments,
        IDeploymentProcessRunner processRunner)
    {
        var arguments = new List<string>(ssh.CreateArguments())
        {
            $"{options.User}@{options.Host}",
        };
        arguments.AddRange(remoteArguments);
        return processRunner.Run(
            "ssh",
            arguments,
            options.RepositoryRoot,
            MaxOutputCharacters,
            TimeSpan.FromMinutes(10));
    }

    private static void RunRequired(
        IDeploymentProcessRunner processRunner,
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory)
    {
        DeploymentProcessResult result = processRunner.Run(
            fileName,
            arguments,
            workingDirectory,
            MaxOutputCharacters,
            TimeSpan.FromMinutes(20));
        Console.Write(result.StandardOutput);
        if (result.ExitCode != 0)
        {
            string diagnostic = string.IsNullOrWhiteSpace(result.StandardError)
                ? result.StandardOutput.Trim()
                : result.StandardError.Trim();
            throw new InvalidOperationException(
                $"'{fileName}' failed with exit code {result.ExitCode}: {diagnostic}");
        }
    }

    private static string CreateRemoteCandidatePath()
    {
        return "/tmp/dotsider-deploy-host-" + Guid.NewGuid().ToString("N");
    }

    private static string QuoteRsyncShellArgument(string value)
    {
        if (value.Contains('\'', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("SSH option path contains an unsupported quote.");
        }

        return $"'{value}'";
    }
}
