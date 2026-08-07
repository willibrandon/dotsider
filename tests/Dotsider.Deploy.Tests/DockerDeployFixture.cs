using System.Diagnostics;

namespace Dotsider.Deploy.Tests;

/// <summary>
/// Provisions the real Debian 13 deployment stack inside a systemd container.
/// Docker credentials are isolated from the user's configuration for every invocation.
/// Published test artifacts remain beneath the repository artifacts directory.
/// </summary>
internal sealed class DockerDeployFixture : IDisposable
{
    private const int OutputLimit = 512 * 1024;
    private readonly string _repositoryRoot;
    private readonly string _dockerConfig;
    private readonly string _builderName = "dotsider-deploy-builder-" + Guid.NewGuid().ToString("N");
    private readonly string _imageName = "dotsider-deploy-tests:" + Guid.NewGuid().ToString("N");
    private readonly string _containerName = "dotsider-deploy-tests-" + Guid.NewGuid().ToString("N");
    private bool _builderCreated;
    private bool _containerCreated;

    /// <summary>
    /// Creates a fixture rooted at the current repository.
    /// The isolated Docker configuration begins empty to avoid credential-helper side effects.
    /// No container or image is created until initialization.
    /// </summary>
    /// <param name="repositoryRoot">The repository root.</param>
    internal DockerDeployFixture(string repositoryRoot)
    {
        _repositoryRoot = repositoryRoot;
        _dockerConfig = Path.Combine(Path.GetTempPath(), "dotsider-docker-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dockerConfig);
        File.WriteAllText(Path.Combine(_dockerConfig, "config.json"), "{}\n", new System.Text.UTF8Encoding(false));
    }

    /// <summary>
    /// Publishes Linux artifacts, starts Debian with systemd, and provisions the host.
    /// A dedicated BuildKit builder isolates the image build from the default builder cache.
    /// Completion leaves the full deployment ready for independent assertions.
    /// </summary>
    internal void Initialize()
    {
        string artifacts = Path.Combine(_repositoryRoot, "artifacts", "deploy-tests");
        if (Directory.Exists(artifacts))
        {
            Directory.Delete(artifacts, recursive: true);
        }

        Directory.CreateDirectory(artifacts);
        RunRequired(
            "dotnet",
            [
                "publish",
                "src/Dotsider.DeployHost/Dotsider.DeployHost.csproj",
                "-c",
                "Release",
                "-r",
                "linux-x64",
                "--self-contained",
                "--artifacts-path",
                Path.Combine(artifacts, "build", "deploy-host"),
                "-o",
                artifacts,
            ]);
        RunRequired(
            "dotnet",
            [
                "publish",
                "src/Dotsider.Website/Dotsider.Website.csproj",
                "-c",
                "Release",
                "-r",
                "linux-x64",
                "--self-contained",
                "-p:PublishSingleFile=true",
                "--artifacts-path",
                Path.Combine(artifacts, "build", "website"),
                "-o",
                Path.Combine(artifacts, "website"),
            ]);
        RunRequired(
            "dotnet",
            [
                "publish",
                "samples/RichLibrary/RichLibrary.csproj",
                "-c",
                "Release",
                "-r",
                "linux-x64",
                "--artifacts-path",
                Path.Combine(artifacts, "build", "sample"),
                "-o",
                Path.Combine(artifacts, "sample"),
            ]);
        RunRequired(
            "docker",
            ["buildx", "create", "--name", _builderName, "--driver", "docker-container"]);
        _builderCreated = true;
        try
        {
            RunRequired(
                "docker",
                [
                    "buildx",
                    "build",
                    "--builder",
                    _builderName,
                    "--load",
                    "--pull",
                    "--no-cache",
                    "-t",
                    _imageName,
                    "-f",
                    "tests/deploy/Dockerfile",
                    ".",
                ]);
        }
        finally
        {
            RemoveBuilder();
        }

        RunRequired(
            "docker",
            ["run", "-d", "--privileged", "--name", _containerName, _imageName]);
        _containerCreated = true;
        WaitForSystemd();
        ExecRequired("/opt/test/dotsider-deploy-host", "provision");
        ExecRequired("/usr/bin/cp", "-a", "/opt/test/website/.", "/opt/dotsider-website/");
        ExecRequired("/usr/bin/cp", "-a", "/opt/test/sample/.", "/opt/dotsider-website/sample/");
        ExecRequired("/usr/bin/chmod", "0755", "/opt/dotsider-website/Dotsider.Website");
        ExecRequired("/usr/bin/chown", "-R", "brandon:brandon", "/opt/dotsider-website");
        DockerResult activation = Exec("/opt/test/dotsider-deploy-host", "activate");
        if (activation.ExitCode != 0)
        {
            DockerResult status = Exec("systemctl", "status", "dotsider-website", "--no-pager");
            DockerResult journal = Exec("journalctl", "-u", "dotsider-website", "--no-pager", "-n", "100");
            throw new InvalidOperationException(
                "Deployment activation failed:"
                + Environment.NewLine
                + activation.StandardError.Trim()
                + Environment.NewLine
                + status.StandardOutput.Trim()
                + Environment.NewLine
                + journal.StandardOutput.Trim());
        }
    }

    /// <summary>
    /// Runs a command in the initialized deployment container.
    /// Arguments retain their literal Docker exec boundaries.
    /// The result is returned without enforcing a successful exit code.
    /// </summary>
    /// <param name="arguments">The executable and arguments inside the container.</param>
    /// <returns>The completed Docker result.</returns>
    internal DockerResult Exec(params string[] arguments)
    {
        var dockerArguments = new List<string> { "exec", _containerName };
        dockerArguments.AddRange(arguments);
        return Run("docker", dockerArguments);
    }

    /// <summary>
    /// Stops and removes the builder, container, image, and isolated Docker configuration.
    /// Cleanup accepts already-removed resources and never mutates the user's Docker files.
    /// Repository artifacts are retained for test diagnostics.
    /// </summary>
    public void Dispose()
    {
        if (_containerCreated)
        {
            _ = Run("docker", ["rm", "-f", _containerName]);
        }

        _ = Run("docker", ["image", "rm", "-f", _imageName]);
        RemoveBuilder();
        if (Directory.Exists(_dockerConfig))
        {
            Directory.Delete(_dockerConfig, recursive: true);
        }
    }

    private void WaitForSystemd()
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            DockerResult result = Exec("systemctl", "is-system-running");
            string state = result.StandardOutput.Trim();
            if (state is "running" or "degraded")
            {
                return;
            }

            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

        throw new InvalidOperationException("The Debian test container did not finish starting systemd.");
    }

    private void ExecRequired(params string[] arguments)
    {
        DockerResult result = Exec(arguments);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Container command '{arguments[0]}' failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
        }
    }

    private void RemoveBuilder()
    {
        if (!_builderCreated)
        {
            return;
        }

        DockerResult result = Run("docker", ["buildx", "rm", "--force", _builderName]);
        _builderCreated = result.ExitCode != 0;
    }

    private void RunRequired(string fileName, IReadOnlyList<string> arguments)
    {
        DockerResult result = Run(fileName, arguments);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'{fileName}' failed with exit code {result.ExitCode}:{Environment.NewLine}"
                + result.StandardOutput.Trim()
                + Environment.NewLine
                + result.StandardError.Trim());
        }
    }

    private DockerResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = _repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        process.StartInfo.Environment["DOCKER_CONFIG"] = _dockerConfig;
        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start '{fileName}'.");
        }

        Task<string> outputTask = ReadBoundedAsync(process.StandardOutput);
        Task<string> errorTask = ReadBoundedAsync(process.StandardError);
        process.WaitForExit();
        Task.WaitAll(outputTask, errorTask);
        return new DockerResult(process.ExitCode, outputTask.Result, errorTask.Result);
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        string value = await reader.ReadToEndAsync().ConfigureAwait(false);
        return value.Length <= OutputLimit ? value : value[..OutputLimit] + "\n[output truncated]\n";
    }
}
