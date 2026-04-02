using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// CLI integration tests that invoke the dotsider process to verify
/// argument parsing, output formatting, and error handling.
/// </summary>
[Collection("SampleAssemblies")]
public class CliTests(SampleAssemblyFixture fixture)
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

    // --- Default analyze output ---

    [Fact]
    public async Task Analyze_Default_ListsTypesMethodsAndReferences()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", fixture.HelloWorldDll);

        Assert.Equal(0, exitCode);
        Assert.Contains("Types (", stdout);
        Assert.Contains("Methods (", stdout);
        Assert.Contains("References (", stdout);
        // Should list actual items, not just counts
        Assert.Contains("Program", stdout);
        Assert.Contains("System.Runtime", stdout);
    }

    // --- P1: --output safety ---

    [Fact]
    public async Task Analyze_MissingInput_DoesNotTruncateOutputFile()
    {
        var outputFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(outputFile, "original content");

            var (exitCode, _, stderr) = await RunDotsiderAsync(
                "analyze", "nonexistent-assembly.dll", "-o", outputFile);

            Assert.NotEqual(0, exitCode);
            Assert.Contains("File not found", stderr);
            Assert.Equal("original content", File.ReadAllText(outputFile));
        }
        finally
        {
            File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task Analyze_SameInputAndOutput_RejectsWithError()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", fixture.HelloWorldDll, "-o", fixture.HelloWorldDll);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Output path cannot be the same as the input file", stderr);
    }

    [Fact]
    public async Task Analyze_InvalidOutputPath_ProducesControlledError()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", fixture.HelloWorldDll, "-o", "/nonexistent/dir/report.txt");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Error:", stderr);
    }

    [Fact]
    public async Task Analyze_ValidOutput_WritesToFile()
    {
        var outputFile = Path.GetTempFileName();
        try
        {
            var (exitCode, stdout, _) = await RunDotsiderAsync(
                "analyze", fixture.HelloWorldDll, "--types", "-o", outputFile);

            Assert.Equal(0, exitCode);
            // stdout should be empty when writing to file
            Assert.Empty(stdout.Trim());
            // File should have the table
            var content = File.ReadAllText(outputFile);
            Assert.Contains("Namespace", content);
            Assert.Contains("Program", content);
        }
        finally
        {
            File.Delete(outputFile);
        }
    }

    // --- P2: TUI option ordering ---

    [Fact]
    public async Task TuiMode_OptionsBeforeFile_RoutesToTuiMode()
    {
        // "--tab 2 <file>" should enter TUI mode, not fall through to subcommand parser.
        // We use a nonexistent file to avoid actually launching the TUI — the key assertion
        // is that we get "File not found" from RunTui, not a System.CommandLine parse error.
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "--tab", "2", "nonexistent-assembly.dll");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        // Should NOT contain System.CommandLine error text
        Assert.DoesNotContain("Required command was not provided", stderr);
    }

    [Fact]
    public async Task TuiMode_OptionsAfterFile_StillWork()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "nonexistent-assembly.dll", "--tab", "2");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        Assert.DoesNotContain("Required command was not provided", stderr);
    }

    // --- P2: --escape-timeout option routing ---

    [Fact]
    public async Task TuiMode_EscapeTimeoutOption_RoutesToTuiMode()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "--escape-timeout", "200", "nonexistent-assembly.dll");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        Assert.DoesNotContain("Required command was not provided", stderr);
    }

    [Fact]
    public async Task TuiMode_ShortEscapeTimeoutAlias_RoutesToTuiMode()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "-e", "200", "nonexistent-assembly.dll");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        Assert.DoesNotContain("Required command was not provided", stderr);
    }

    [Fact]
    public async Task DiffMode_EscapeTimeoutOption_Accepted()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "diff", "--escape-timeout", "200", "nonexistent-left.dll", "nonexistent-right.dll");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        Assert.DoesNotContain("Unrecognized", stderr);
    }

    [Fact]
    public async Task DiffMode_ShortEscapeTimeoutAlias_Accepted()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "diff", "-e", "200", "nonexistent-left.dll", "nonexistent-right.dll");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("File not found", stderr);
        Assert.DoesNotContain("Unrecognized", stderr);
    }

    // --- No-args exit code (WinGet validation) ---

    [Fact]
    public async Task NoArgs_ShowsHelpAndReturnsZero()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync();

        Assert.Equal(0, exitCode);
        Assert.Contains("dotsider", stdout);
        Assert.Contains("Commands:", stdout);
    }

    [Fact]
    public async Task JsonFlagAlone_ReturnsNonZero()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync("--json");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Required command was not provided", stderr);
    }

    // --- Apphost Detection ---

    [Fact]
    public async Task Analyze_Apphost_AutoRedirectsToManagedDll()
    {
        var (exitCode, stdout, stderr) = await RunDotsiderAsync(
            "analyze", fixture.HelloWorldExe);

        Assert.Equal(0, exitCode);
        Assert.Contains("apphost", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HelloWorld", stdout);
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

    private static string QuoteArg(string arg)
        => arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
