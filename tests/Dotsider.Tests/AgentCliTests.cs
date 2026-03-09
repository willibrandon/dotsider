using System.Diagnostics;

namespace Dotsider.Tests;

/// <summary>
/// CLI integration tests for the agent command.
/// </summary>
public class AgentCliTests
{
    private static readonly string s_projectPath = Path.Combine(
        TestHelpers.GetRepoRoot(), "src", "Dotsider");

    private static readonly string s_buildConfig = DetectBuildConfig();

    private static string DetectBuildConfig()
    {
        var parts = AppContext.BaseDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("bin", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }

        return "Debug";
    }

    [Fact]
    public async Task Agent_Init_Stdout_WritesSkillContent()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "agent", "init", "--stdout");

        Assert.Equal(0, exitCode);
        Assert.Contains("name: dotsider", stdout);
        Assert.Contains("dotsider analyze", stdout);
        Assert.Contains("dotsider sessions", stdout);
    }

    [Fact]
    public async Task Agent_Init_WithPath_CreatesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(tempDir, "SKILL.md");

        try
        {
            var (exitCode, stdout, _) = await RunDotsiderAsync(
                "agent", "init", "--path", outputPath);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));
            var content = File.ReadAllText(outputPath);
            Assert.Contains("name: dotsider", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
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

            Assert.NotEqual(0, exitCode);
            Assert.Contains("already exists", stderr);
            Assert.Equal("existing content", File.ReadAllText(outputPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
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

            Assert.Equal(0, exitCode);
            var content = File.ReadAllText(outputPath);
            Assert.Contains("name: dotsider", content);
            Assert.DoesNotContain("existing content", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Agent_Init_WithAi_CreatesCorrectPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDir);

            // --ai claude resolves .claude/skills/dotsider/SKILL.md relative to cwd
            var (exitCode, stdout, _) = await RunDotsiderInDirAsync(
                tempDir, "agent", "init", "--ai", "claude");

            Assert.Equal(0, exitCode);

            var expectedPath = Path.Combine(tempDir, ".claude", "skills", "dotsider", "SKILL.md");
            Assert.True(File.Exists(expectedPath), $"Expected file at {expectedPath}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Agent_Init_NoArgs_ShowsUsageError()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "agent", "init");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("--ai", stderr);
        Assert.Contains("--path", stderr);
        Assert.Contains("--stdout", stderr);
    }

    [Fact]
    public async Task Agent_Help_ShowsSubcommands()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "agent", "--help");

        Assert.Equal(0, exitCode);
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

        var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout, stderr);
    }

    private static string QuoteArg(string arg)
        => arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
