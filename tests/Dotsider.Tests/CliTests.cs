using System.Diagnostics;

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

    // --- Fields ---

    /// <summary>Verifies that --fields lists field definitions in text mode.</summary>
    [Fact]
    public async Task Analyze_Fields_ListsFieldDefinitions()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", fixture.RichLibraryDll, "--fields");

        Assert.Equal(0, exitCode);
        Assert.Contains("Namespace", stdout);
        Assert.Contains("Name", stdout);
        Assert.Contains("Signature", stdout);
    }

    /// <summary>Verifies that --fields with --json outputs a JSON array.</summary>
    [Fact]
    public async Task Analyze_Fields_Json_OutputsJson()
    {
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", fixture.RichLibraryDll, "--fields", "--json");

        Assert.Equal(0, exitCode);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(stdout);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, json.ValueKind);
        Assert.True(json.GetArrayLength() > 0);
    }

    // --- Bundle ---

    /// <summary>Verifies that --bundle shows the manifest for a single-file bundle.</summary>
    [Fact]
    public async Task Analyze_Bundle_ShowsManifest()
    {
        Assert.NotNull(fixture.SelfContainedConsoleExe);
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", fixture.SelfContainedConsoleExe!, "--bundle");

        Assert.Equal(0, exitCode);
        Assert.Contains("Bundle version:", stdout);
        Assert.Contains("Entries:", stdout);
    }

    /// <summary>Verifies that --bundle with --json outputs structured manifest data.</summary>
    [Fact]
    public async Task Analyze_Bundle_Json_OutputsJson()
    {
        Assert.NotNull(fixture.SelfContainedConsoleExe);
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", fixture.SelfContainedConsoleExe!, "--bundle", "--json");

        Assert.Equal(0, exitCode);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(stdout);
        Assert.True(json.GetProperty("fileCount").GetInt32() > 0);
    }

    /// <summary>Verifies that --bundle on a non-bundle file returns an error.</summary>
    [Fact]
    public async Task Analyze_Bundle_NonBundle_ReturnsError()
    {
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", fixture.RichLibraryDll, "--bundle");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("not a single-file bundle", stderr);
    }

    /// <summary>Verifies that --bundle -o rejects writing to the same input file.</summary>
    [Fact]
    public async Task Analyze_Bundle_SameInputAndOutput_RejectsWithError()
    {
        Assert.NotNull(fixture.SelfContainedConsoleExe);
        var (exitCode, _, stderr) = await RunDotsiderAsync(
            "analyze", fixture.SelfContainedConsoleExe!, "--bundle", "-o", fixture.SelfContainedConsoleExe!);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Output path cannot be the same as the input file", stderr);
    }

    /// <summary>Verifies that default output for a bundle-backed assembly shows DisplayName.</summary>
    [Fact]
    public async Task Analyze_DefaultOutput_BundleBacked_ShowsDisplayName()
    {
        Assert.NotNull(fixture.SelfContainedConsoleExe);
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", fixture.SelfContainedConsoleExe!);

        Assert.Equal(0, exitCode);
        Assert.Contains("from bundle", stdout);
    }

    /// <summary>Verifies that default JSON output for a bundle-backed assembly includes bundle properties.</summary>
    [Fact]
    public async Task Analyze_DefaultOutput_BundleBacked_Json_IncludesProperties()
    {
        Assert.NotNull(fixture.SelfContainedConsoleExe);
        var (exitCode, stdout, _) = await RunDotsiderAsync(
            "analyze", fixture.SelfContainedConsoleExe!, "--json");

        Assert.Equal(0, exitCode);
        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(stdout);
        Assert.True(json.GetProperty("isBundleBacked").GetBoolean());
        Assert.Equal("SelfContainedConsole.dll", json.GetProperty("displayName").GetString());
        Assert.NotNull(json.GetProperty("preferredRuntimePack").GetString());
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
