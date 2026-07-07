using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Dotsider.Tests;

/// <summary>
/// Verifies repository utility scripts follow the file-based app conventions.
/// The checks mirror the repository's Picket-style script policy.
/// New file-based apps stay documented, buildable, and friendly to editor hovers.
/// </summary>
public sealed partial class ScriptConventionTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "dotsider-script-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Cleans up temporary script output.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    /// Verifies the script README documents the file-based app workflow.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ScriptsReadme_DocumentsFileBasedAppWorkflow()
    {
        string root = FindRepositoryRoot();
        string readme = File.ReadAllText(Path.Combine(root, "scripts", "README.md"));
        string attributes = File.ReadAllText(Path.Combine(root, ".gitattributes"));

        Assert.Contains("dotnet run --file ./scripts/Capture-DisasmOracle.cs", readme);
        Assert.Contains("dotnet build ./scripts/Capture-DisasmOracle.cs", readme);
        Assert.Contains("dotnet clean file-based-apps", readme);
        Assert.Contains("#!/usr/bin/env -S dotnet --", readme);
        Assert.Contains("documented app", readme);
        Assert.Contains("Native architecture oracles", readme);
        Assert.Contains("run-runtime-cross-target", readme);
        Assert.Contains("scripts/*.cs text eol=lf", attributes);
    }

    /// <summary>
    /// Verifies the native architecture oracle workflow uses the file-based capture app.
    /// The outer-loop workflow should refresh artifacts without changing normal PR CI cost.
    /// Runtime cross-target entries stay pinned to the same container family as dotnet/runtime.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeArchitectureOracleWorkflow_UsesCaptureAppAndRuntimeCrossImages()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "native-arch-oracles.yml"));

        Assert.Contains("Capture-DisasmOracle.cs", workflow);
        Assert.Contains("dotnet workload install wasm-tools", workflow);
        Assert.Contains("azurelinux-3.0-net11.0-cross-riscv64", workflow);
        Assert.Contains("azurelinux-3.0-net11.0-cross-loongarch64", workflow);
        Assert.Contains("run-runtime-cross-target", workflow);
    }

    /// <summary>
    /// Verifies the disassembly oracle app builds and captures stable fake input.
    /// </summary>
    [Fact]
    public void CaptureDisasmOracle_BuildsAndCapturesFakeInput()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "Capture-DisasmOracle.cs");
        string outputDirectory = Path.Combine(_tempRoot, "oracles");

        var (exitCode, _, _) = RunDotnet(
            root,
            "run",
            "--file",
            scriptPath,
            "--",
            "-Architecture",
            "test",
            "-Fixture",
            "README.md",
            "-OraclePath",
            "dotnet",
            "-OutputDirectory",
            outputDirectory,
            "--",
            "--version");

        Assert.Equal(0, exitCode);
        string stdoutPath = Path.Combine(outputDirectory, "README.test.oracle.txt");
        string metadataPath = Path.Combine(outputDirectory, "README.test.oracle.json");
        Assert.True(File.Exists(stdoutPath), stdoutPath);
        Assert.True(File.Exists(metadataPath), metadataPath);
        Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(stdoutPath)));
        string metadata = File.ReadAllText(metadataPath);
        Assert.Contains("\"Architecture\": \"test\"", metadata);
        Assert.Contains("\"FixtureSha256\"", metadata);
        Assert.Contains("\"OracleArguments\"", metadata);
    }

    /// <summary>
    /// Verifies oracle capture can retain bounded output without failing.
    /// Large disassembly tools can produce very large streams in CI.
    /// The capture metadata records truncation instead of growing memory without bound.
    /// </summary>
    [Fact]
    public void CaptureDisasmOracle_TruncatesLargeOutput()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "Capture-DisasmOracle.cs");
        string outputDirectory = Path.Combine(_tempRoot, "truncated-oracles");

        var (exitCode, _, _) = RunDotnet(
            root,
            "run",
            "--file",
            scriptPath,
            "--",
            "-Architecture",
            "test",
            "-Fixture",
            "README.md",
            "-OraclePath",
            "dotnet",
            "-OutputDirectory",
            outputDirectory,
            "-MaxOutputCharacters",
            "20",
            "--",
            "--info");

        Assert.Equal(0, exitCode);
        string stdout = File.ReadAllText(Path.Combine(outputDirectory, "README.test.oracle.txt"));
        string metadata = File.ReadAllText(Path.Combine(outputDirectory, "README.test.oracle.json"));
        Assert.Contains("[output truncated after 20 characters]", stdout);
        Assert.Contains("\"StdoutTruncated\": true", metadata);
    }

    /// <summary>
    /// Verifies allowed oracle failures still produce reviewable artifacts.
    /// Outer-loop capture workflows should upload evidence from failed oracle tools.
    /// The script exits successfully only when the caller explicitly allows the oracle failure.
    /// </summary>
    [Fact]
    public void CaptureDisasmOracle_AllowsOracleFailure()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "Capture-DisasmOracle.cs");
        string outputDirectory = Path.Combine(_tempRoot, "failure-oracles");

        var (exitCode, _, _) = RunDotnet(
            root,
            "run",
            "--file",
            scriptPath,
            "--",
            "-Architecture",
            "test",
            "-Fixture",
            "README.md",
            "-OraclePath",
            "dotnet",
            "-OutputDirectory",
            outputDirectory,
            "-AllowOracleFailure",
            "--",
            "definitely-not-a-dotnet-command");

        Assert.Equal(0, exitCode);
        string metadata = File.ReadAllText(Path.Combine(outputDirectory, "README.test.oracle.json"));
        Assert.Contains("\"OracleExitCode\":", metadata);
        Assert.DoesNotContain("\"OracleExitCode\": 0", metadata);
    }

    /// <summary>
    /// Verifies new top-level utility and decoder test types have three-line summaries.
    /// This keeps internal helper types documented enough for editor hovers.
    /// The file list is intentionally scoped to the new file-based app and architecture work.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NewTopLevelTypes_HaveThreeLineSummaries()
    {
        string root = FindRepositoryRoot();
        string[] relativePaths =
        [
            "scripts/Capture-DisasmOracle.cs",
            "scripts/ScriptSupport.cs",
            "src/Dotsider.Core/Analysis/Disasm/NativeDecoderRegistry.cs",
            "src/Dotsider.Core/Analysis/Disasm/NativeDecoderSupport.cs",
            "src/Dotsider.Core/Analysis/Disasm/x86/X86Decoder.cs",
            "src/Dotsider.Core/Analysis/Disasm/arm32/Arm32ThumbDecoder.cs",
            "src/Dotsider.Core/Analysis/Disasm/riscv64/RiscV64Decoder.cs",
            "src/Dotsider.Core/Analysis/Disasm/loongarch64/LoongArch64Decoder.cs",
            "src/Dotsider.Core/Analysis/Disasm/wasm32/Wasm32Decoder.cs",
            "tests/Dotsider.Tests/NativeArchitectureDecoderTests.cs",
            "tests/Dotsider.Tests/NativeDecoderRegistryTests.cs",
            "tests/Dotsider.Tests/NativeDisasmFixtureGoldenTests.cs",
            "tests/Dotsider.Tests/ReadyToRunArchitectureDecoderTests.cs",
            "tests/Dotsider.Tests/ScriptConventionTests.cs",
        ];

        foreach (string relativePath in relativePaths)
        {
            string text = File.ReadAllText(Path.Combine(root, relativePath));
            Assert.True(HasThreeLineSummaryBeforeFirstType(text), relativePath);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dotsider.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static (int ExitCode, string Stdout, string Stderr) RunDotnet(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
        }

        return (process.ExitCode, stdout, stderr);
    }

    private static bool HasThreeLineSummaryBeforeFirstType(string text)
    {
        Match typeMatch = TopLevelTypeRegex().Match(text);
        Assert.True(typeMatch.Success, "No top-level type declaration found.");

        string beforeType = text[..typeMatch.Index];
        MatchCollection summaries = SummaryRegex().Matches(beforeType);
        if (summaries.Count == 0)
        {
            return false;
        }

        string body = summaries[^1].Groups["body"].Value;
        return body.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Length >= 3;
    }

    [GeneratedRegex(@"(?m)^(?:internal|public)\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+)*(?:class|record|struct|enum|interface)\s+")]
    private static partial Regex TopLevelTypeRegex();

    [GeneratedRegex(@"/// <summary>\r?\n(?<body>(?:/// .+\r?\n)+)/// </summary>", RegexOptions.CultureInvariant)]
    private static partial Regex SummaryRegex();
}
