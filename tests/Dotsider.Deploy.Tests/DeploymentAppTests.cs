namespace Dotsider.Deploy.Tests;

/// <summary>
/// Verifies local deployment orchestration and failure cleanup without a remote host.
/// Process calls use an injected runner so literal SSH and rsync boundaries remain observable.
/// Environment credentials and temporary files are restored after each test.
/// </summary>
[TestClass]
public sealed class DeploymentAppTests
{
    /// <summary>
    /// Forces host-key discovery to fail before an SSH identity is created.
    /// The operation must return a failure without leaving a credential directory.
    /// No SSH or SCP process may run after discovery fails.
    /// </summary>
    [TestMethod]
    public void Run_HostKeyScanFails_DoesNotCreateCredentialFilesOrConnect()
    {
        string root = Path.Combine(Path.GetTempPath(), "dotsider-deploy-app-test-" + Guid.NewGuid().ToString("N"));
        string? originalKey = Environment.GetEnvironmentVariable("DEPLOY_SSH_KEY");
        Directory.CreateDirectory(root);
        string deployHost = Path.Combine(root, "dotsider-deploy-host");
        File.WriteAllText(deployHost, "candidate");
        string[] temporaryDirectoriesBefore = Directory.GetDirectories(Path.GetTempPath(), "dotsider-deploy-*");
        var runner = new StubDeploymentProcessRunner(
            static (_, _) => Result(1, standardError: "scan failed"));

        try
        {
            Environment.SetEnvironmentVariable("DEPLOY_SSH_KEY", "test-key");

            int exitCode = DeploymentApp.Run(
                ["-Mode", "Preflight", "-Host", "example.test", "-DeployHost", deployHost],
                runner);

            Assert.AreEqual(1, exitCode);
            Assert.HasCount(1, runner.Calls);
            Assert.AreEqual("ssh-keyscan", runner.Calls[0].FileName);
            Assert.AreSequenceEqual(
                temporaryDirectoriesBefore,
                Directory.GetDirectories(Path.GetTempPath(), "dotsider-deploy-*"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEPLOY_SSH_KEY", originalKey);
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// Forces a website rsync failure after an active integrity timer is stopped.
    /// The finally path must restart the timer and remove temporary SSH credentials.
    /// This protects the production recovery guarantee independently of Docker setup tests.
    /// </summary>
    [TestMethod]
    public void Run_DeployRsyncFails_RestartsPreviouslyActiveIntegrityTimer()
    {
        string root = Path.Combine(Path.GetTempPath(), "dotsider-deploy-app-test-" + Guid.NewGuid().ToString("N"));
        string? originalKey = Environment.GetEnvironmentVariable("DEPLOY_SSH_KEY");
        Directory.CreateDirectory(root);
        string deployHost = Path.Combine(root, "dotsider-deploy-host");
        string docs = Directory.CreateDirectory(Path.Combine(root, "docs")).FullName;
        string website = Directory.CreateDirectory(Path.Combine(root, "website")).FullName;
        string sample = Directory.CreateDirectory(Path.Combine(root, "sample")).FullName;
        File.WriteAllText(deployHost, "candidate");
        string[] temporaryDirectoriesBefore = Directory.GetDirectories(Path.GetTempPath(), "dotsider-deploy-*");
        var rsyncCount = 0;
        var runner = new StubDeploymentProcessRunner((fileName, _) =>
        {
            if (fileName == "ssh-keyscan")
            {
                return Result(0, "example.test ssh-ed25519 AAAATEST\n");
            }

            if (fileName == "rsync" && ++rsyncCount == 2)
            {
                return Result(12, standardError: "rsync failed");
            }

            return Result(0);
        });

        try
        {
            Environment.SetEnvironmentVariable("DEPLOY_SSH_KEY", "test-key");

            int exitCode = DeploymentApp.Run(
                [
                    "-Mode",
                    "Deploy",
                    "-Host",
                    "example.test",
                    "-DeployHost",
                    deployHost,
                    "-Docs",
                    docs,
                    "-Website",
                    website,
                    "-Sample",
                    sample,
                ],
                runner);

            Assert.AreEqual(1, exitCode);
            Assert.Contains(static call => call.FileName == "ssh"
                && call.Arguments.Contains("start", StringComparer.Ordinal)
                && call.Arguments.Contains("integrity-check.timer", StringComparer.Ordinal), runner.Calls);
            Assert.AreSequenceEqual(
                temporaryDirectoriesBefore,
                Directory.GetDirectories(Path.GetTempPath(), "dotsider-deploy-*"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEPLOY_SSH_KEY", originalKey);
            Directory.Delete(root, recursive: true);
        }
    }

    private static DeploymentProcessResult Result(
        int exitCode,
        string standardOutput = "",
        string standardError = "")
    {
        return new DeploymentProcessResult(
            exitCode,
            standardOutput,
            standardError,
            StandardOutputTruncated: false,
            StandardErrorTruncated: false,
            TimedOut: false);
    }
}
