using System.Diagnostics;

namespace Dotsider.Tests;

/// <summary>
/// CLI integration tests for the agent command.
/// </summary>
[TestClass]
public sealed class AgentCliTests
{
    private static readonly string s_projectPath = Path.Combine(
        TestHelpers.GetRepoRoot(), "src", "Dotsider");

    private static readonly string s_buildConfig = TestProcessEnvironment.CurrentBuildConfiguration;

    /// <summary>
    /// Verifies agent init stdout writes skill content.
    /// </summary>
    [TestMethod]
    public async Task Agent_Init_Stdout_WritesSkillContent()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "agent", "init", "--stdout");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("name: dotsider", stdout);
        Assert.Contains("dotsider analyze", stdout);
        Assert.Contains("dotsider sessions", stdout);
    }

    /// <summary>
    /// Verifies agent init with path creates file.
    /// </summary>
    [TestMethod]
    public async Task Agent_Init_WithPath_CreatesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(tempDir, "SKILL.md");

        try
        {
            var (exitCode, stdout, _) = await RunDotsiderAsync(
                "agent", "init", "--path", outputPath);

            Assert.AreEqual(0, exitCode);
            Assert.IsTrue(File.Exists(outputPath));
            var content = File.ReadAllText(outputPath);
            Assert.Contains("name: dotsider", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    /// Verifies agent init no force errors if exists.
    /// </summary>
    [TestMethod]
    public async Task Agent_Init_NoForce_ErrorsIfExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(tempDir, "SKILL.md");

        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(outputPath, "existing content");

            var (exitCode, _, stderr) = await RunDotsiderAsync(
                "agent", "init", "--path", outputPath);

            Assert.AreNotEqual(0, exitCode);
            Assert.Contains("already exists", stderr);
            Assert.AreEqual("existing content", File.ReadAllText(outputPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    /// Verifies agent init force overwrites existing.
    /// </summary>
    [TestMethod]
    public async Task Agent_Init_Force_OverwritesExisting()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(tempDir, "SKILL.md");

        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(outputPath, "existing content");

            var (exitCode, stdout, _) = await RunDotsiderAsync(
                "agent", "init", "--path", outputPath, "--force");

            Assert.AreEqual(0, exitCode);
            var content = File.ReadAllText(outputPath);
            Assert.Contains("name: dotsider", content);
            Assert.DoesNotContain("existing content", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    /// Verifies agent init creates SKILL.md in the current directory by default.
    /// </summary>
    [TestMethod]
    public async Task Agent_Init_NoOptions_CreatesSkillInCurrentDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);

            var (exitCode, stdout, _) = await RunDotsiderInDirAsync(
                tempDir, "agent", "init");

            Assert.AreEqual(0, exitCode);
            var expectedPath = Path.Combine(tempDir, "SKILL.md");
            Assert.IsTrue(File.Exists(expectedPath), $"Expected file at {expectedPath}");
            Assert.StartsWith("Created: ", stdout);
            var createdPath = stdout["Created: ".Length..].Trim();
            Assert.AreEqual("SKILL.md", Path.GetFileName(createdPath));
            Assert.IsTrue(File.Exists(createdPath), $"Expected reported file at {createdPath}");
            Assert.Contains("name: dotsider", File.ReadAllText(expectedPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    /// Verifies agent help shows subcommands.
    /// </summary>
    [TestMethod]
    public async Task Agent_Help_ShowsSubcommands()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "agent", "--help");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("mcp", stdout);
        Assert.Contains("init", stdout);
    }

    // --- Helpers ---

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunDotsiderAsync(
        params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build -c {s_buildConfig} --project \"{s_projectPath}\" -- {string.Join(' ', arguments.Select(QuoteArg))}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        TestProcessEnvironment.RemoveCodeCoverageVariables(psi);

        var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout, stderr);
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunDotsiderInDirAsync(
        string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build -c {s_buildConfig} --project \"{s_projectPath}\" -- {string.Join(' ', arguments.Select(QuoteArg))}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory,
        };
        TestProcessEnvironment.RemoveCodeCoverageVariables(psi);

        var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout, stderr);
    }

    private static string QuoteArg(string arg)
        => arg.Contains(' ') ? $"\"{arg}\"" : arg;

    /// <summary>
    /// Deletes a temp directory with retries. On Windows, a process that used the
    /// directory as its CWD may still hold a handle briefly after exit.
    /// </summary>
    private static void CleanupTempDir(string path)
    {
        if (!Directory.Exists(path))
            return;

        for (var i = 0; i < 5; i++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (i < 4)
            {
                Thread.Sleep(200);
            }
        }
    }
}
