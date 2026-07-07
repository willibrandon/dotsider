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
        Assert.Contains("scripts/*.cs text eol=lf", attributes);
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
