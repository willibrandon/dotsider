using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dotsider.Tests;

/// <summary>
/// Verifies repository utility scripts follow the file-based app conventions.
/// The checks mirror the repository's Picket-style script policy.
/// New file-based apps stay documented, buildable, and friendly to editor hovers.
/// </summary>
[TestClass]
public sealed partial class ScriptConventionTests : IDisposable
{
    private static readonly ConcurrentDictionary<string, Lazy<bool>> s_builtFileApps = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock s_fileAppExecutionLock = new();

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
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ScriptsReadme_DocumentsFileBasedAppWorkflow()
    {
        string root = FindRepositoryRoot();
        string readme = File.ReadAllText(Path.Combine(root, "scripts", "README.md"));
        string runTestsScript = File.ReadAllText(Path.Combine(root, "scripts", "Run-Tests.cs"));
        string attributes = File.ReadAllText(Path.Combine(root, ".gitattributes"));

        Assert.Contains("dotnet run --file ./scripts/Capture-DisasmOracle.cs", readme);
        Assert.Contains("dotnet run --file ./scripts/Initialize-DevContainer.cs", readme);
        Assert.Contains("dotnet run --file ./scripts/Run-Tests.cs", readme);
        Assert.Contains("dotnet run --file ./scripts/Verify-NativeAot.cs", readme);
        Assert.Contains("FullyQualifiedName~", runTestsScript);
        Assert.DoesNotContain("dotnet-suggest", runTestsScript);
        Assert.Contains("#!/usr/bin/env -S dotnet --", readme);
        Assert.Contains("Current utilities", readme);
        Assert.Contains("-RuntimeRoot path/to/runtime", readme);
        Assert.Contains("* text=auto", attributes);
        Assert.Contains("scripts/*.cs text eol=lf", attributes);
        Assert.Contains("scripts/**/*.cs text eol=lf", attributes);
        Assert.Contains("#:package System.CommandLine", runTestsScript);
        Assert.DoesNotContain("#:package System.CommandLine@", runTestsScript);
    }

    /// <summary>
    /// Verifies the development container pins the repository toolchain and uses the initializer app.
    /// Native tools, documentation dependencies, Docker isolation, and the Hex1b CLI stay reproducible.
    /// The validation workflow catches upstream image or feature changes before contributors do.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DevContainerConfiguration_ProvidesCompleteLinuxEnvironment()
    {
        string root = FindRepositoryRoot();
        string configuration = File.ReadAllText(Path.Combine(root, ".devcontainer", "devcontainer.json"));
        string dockerfile = File.ReadAllText(Path.Combine(root, ".devcontainer", "Dockerfile"));
        string picketIgnore = File.ReadAllText(Path.Combine(root, ".devcontainer", "picket-image.ignore"));
        string initializer = File.ReadAllText(Path.Combine(root, "scripts", "Initialize-DevContainer.cs"));
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "dev-container.yml"));
        using JsonDocument configurationDocument = JsonDocument.Parse(configuration);
        JsonElement terminalEnvironment = configurationDocument.RootElement
            .GetProperty("customizations")
            .GetProperty("vscode")
            .GetProperty("settings")
            .GetProperty("terminal.integrated.env.linux");

        Assert.Contains("mcr.microsoft.com/devcontainers/base:noble", dockerfile);
        Assert.Contains("clang", dockerfile);
        Assert.Contains("llvm", dockerfile);
        Assert.Contains("zlib1g-dev", dockerfile);
        Assert.Contains("\"version\": \"10.0.302\"", configuration);
        Assert.Contains("\"workloads\": \"wasm-tools\"", configuration);
        Assert.Contains("\"version\": \"22\"", configuration);
        Assert.Contains("\"pnpmVersion\": \"10.28.0\"", configuration);
        Assert.Contains("docker-in-docker:4", configuration);
        Assert.DoesNotContain("docker-outside-of-docker", configuration);
        Assert.Contains("Demo__SampleAssembly", configuration);
        Assert.Contains("\"DOTSIDER_DEV_CONTAINER\": \"1\"", configuration);
        string buildProperties = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        Assert.Contains("<BaseOutputPath>bin/devcontainer/</BaseOutputPath>", buildProperties);
        Assert.Contains("<BaseIntermediateOutputPath>obj/devcontainer/</BaseIntermediateOutputPath>", buildProperties);
        Assert.Contains(
            "$(MSBuildProjectDirectory)/artifacts/devcontainer",
            buildProperties);
        Assert.Contains(
            "<DefaultItemExcludes>$(DefaultItemExcludes);obj/**;bin/**;artifacts/**</DefaultItemExcludes>",
            buildProperties);
        Assert.Contains("'$(DOTSIDER_FIXTURE_BUILD)' != '1'", buildProperties);
        Assert.Contains("'$(FileBasedProgram)' != 'true'", buildProperties);
        Assert.Contains("'$(Configuration)' == 'DevContainerDebug'", buildProperties);
        Assert.Contains("'$(Configuration)' == 'DevContainerRelease'", buildProperties);
        Assert.Contains("<DefineConstants>$(DefineConstants);DEBUG</DefineConstants>", buildProperties);
        Assert.Contains("<Optimize>false</Optimize>", buildProperties);
        Assert.Contains("<Optimize>true</Optimize>", buildProperties);
        Assert.Contains("Initialize-DevContainer.cs", configuration);
        Assert.Contains("\"waitFor\": \"postCreateCommand\"", configuration);
        Assert.Contains("\"dotnet.preferVisualStudioCodeFileSystemWatcher\": true", configuration);
        Assert.AreEqual("4", terminalEnvironment.GetProperty("DOTNET_PROCESSOR_COUNT").GetString());
        Assert.AreEqual("1", terminalEnvironment.GetProperty("MSBUILDDISABLENODEREUSE").GetString());
        Assert.Contains("Directory.Packages.props", initializer);
        Assert.Contains("RestoreFileApps(repositoryRoot)", initializer);
        Assert.Contains("[\"restore\", fileApp, \"--nologo\", \"--verbosity\", \"quiet\"]", initializer);
        Assert.DoesNotContain("[\"build\", fileApp", initializer);
        Assert.Contains("Capture-DisasmOracle.cs", initializer);
        Assert.Contains("Deploy-Website.cs", initializer);
        Assert.Contains("Hex1b.Tool", initializer);
        Assert.DoesNotContain("Hex1b.McpServer", initializer);
        Assert.Contains("Run-Tests.cs", initializer);
        Assert.Contains("Verify-NativeAot.cs", initializer);
        Assert.Contains("safe.directory", initializer);
        Assert.Contains("[\"CI\"] = \"true\"", initializer);
        Assert.Contains("devcontainers/ci@v0.3", workflow);
        Assert.Contains("dotnet clean", workflow);
        Assert.Contains("dotnet build --no-restore", workflow);
        Assert.Contains("dotnet test --no-build", workflow);
        Assert.Contains(
            "DOTSIDER_RUN_DEPLOY_INTEGRATION=1 dotnet test tests/Dotsider.Deploy.Tests/Dotsider.Deploy.Tests.csproj --no-build",
            workflow);
        Assert.Contains("imageName: dotsider-devcontainer", workflow);
        Assert.Contains("imageTag: ci", workflow);
        Assert.Contains("Picket --version 0.2.9", workflow);
        Assert.Contains("--docker-archive ${{ runner.temp }}/dotsider-devcontainer.tar", workflow);
        Assert.Contains("--ignore-path .devcontainer/picket-image.ignore", workflow);
        Assert.Contains("--redact 100", workflow);
        Assert.Contains("--exit-code 1", workflow);
        Assert.Contains("actions/upload-artifact@v7", workflow);
        Assert.Contains("sha256:03aebbff795f9aedefa7c850889fa674e55d5a43b8b1bb8fc711e1cdd6bb3582", picketIgnore);
    }

    /// <summary>
    /// Keeps Linux ARM64 in the full build-and-test matrix so architecture-specific behavior
    /// is exercised on every pull request.
    /// </summary>
    [TestMethod]
    public void ContinuousIntegration_RunsFullSuiteOnLinuxArm64()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));

        Assert.Contains(
            "os: [ubuntu-latest, ubuntu-24.04-arm, windows-latest, macos-26]",
            workflow);
    }

    /// <summary>
    /// Verifies the development container initializer builds and exposes safe usage text.
    /// Help must not restore dependencies or change the developer's global tool installation.
    /// The real initialization path is exercised when CI creates the development container.
    /// </summary>
    [TestMethod]
    public void InitializeDevContainer_BuildsAndPrintsHelp()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "Initialize-DevContainer.cs");

        var (exitCode, stdout, _) = RunFileApp(root, scriptPath, "--help");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("development container", stdout);
        Assert.Contains("Hex1b.Tool", stdout);
        Assert.DoesNotContain("Hex1b.McpServer", stdout);
    }

    /// <summary>
    /// Verifies the native architecture oracle workflow uses the file-based capture app.
    /// The outer-loop workflow should refresh artifacts without changing normal PR CI cost.
    /// Runtime cross-target entries stay pinned to the same container family as dotnet/runtime.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeArchitectureOracleWorkflow_UsesCaptureAppAndRuntimeCrossImages()
    {
        string root = FindRepositoryRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "native-arch-oracles.yml"));

        Assert.Contains("Capture-DisasmOracle.cs", workflow);
        Assert.Contains("dotnet workload install wasm-tools", workflow);
        Assert.Contains("azurelinux-3.0-net11.0-cross-riscv64", workflow);
        Assert.Contains("sha256:7d882ca090cfc0fb146dcb0c4fa97f4bd1b9c160822d0652d21d666897bf7f4f", workflow);
        Assert.Contains("azurelinux-3.0-net11.0-cross-loongarch64", workflow);
        Assert.Contains("sdk-publish.log", workflow);
        Assert.Contains("::notice title=SDK publish unavailable::", workflow);
        Assert.Contains("run-runtime-cross-target", workflow);
    }

    /// <summary>
    /// Verifies the disassembly oracle app builds and captures stable fake input.
    /// </summary>
    [TestMethod]
    public void CaptureDisasmOracle_BuildsAndCapturesFakeInput()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "Capture-DisasmOracle.cs");
        string outputDirectory = Path.Combine(_tempRoot, "oracles");

        var (exitCode, _, _) = RunFileApp(
            root,
            scriptPath,
            "-Architecture",
            "test",
            "-Fixture",
            "README.md",
            "-OraclePath",
            "dotnet",
            "-OutputDirectory",
            outputDirectory,
            "-RuntimeRoot",
            "",
            "--",
            "--version");

        Assert.AreEqual(0, exitCode);
        string stdoutPath = Path.Combine(outputDirectory, "README.test.oracle.txt");
        string metadataPath = Path.Combine(outputDirectory, "README.test.oracle.json");
        Assert.IsTrue(File.Exists(stdoutPath), stdoutPath);
        Assert.IsTrue(File.Exists(metadataPath), metadataPath);
        Assert.IsFalse(string.IsNullOrWhiteSpace(File.ReadAllText(stdoutPath)));
        using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        JsonElement metadataRoot = metadata.RootElement;
        Assert.AreEqual("test", metadataRoot.GetProperty("Architecture").GetString());
        Assert.IsTrue(metadataRoot.TryGetProperty("FixtureSha256", out _));
        Assert.IsTrue(metadataRoot.TryGetProperty("OracleArguments", out _));
        Assert.AreEqual(JsonValueKind.Null, metadataRoot.GetProperty("RuntimeRoot").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, metadataRoot.GetProperty("RuntimeCommit").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, metadataRoot.GetProperty("RuntimeBranch").ValueKind);
    }

    /// <summary>
    /// Verifies an explicitly configured runtime clone is resolved without assuming a host layout.
    /// Runtime provenance must describe the supplied repository and never a machine-specific default.
    /// </summary>
    [TestMethod]
    public void CaptureDisasmOracle_ExplicitRuntimeRoot_RecordsProvenance()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "Capture-DisasmOracle.cs");
        string outputDirectory = Path.Combine(_tempRoot, "runtime-root-oracle");

        var (exitCode, _, _) = RunFileApp(
            root,
            scriptPath,
            "-Architecture",
            "test",
            "-Fixture",
            "README.md",
            "-OraclePath",
            "dotnet",
            "-OutputDirectory",
            outputDirectory,
            "-RuntimeRoot",
            ".",
            "--",
            "--version");

        Assert.AreEqual(0, exitCode);
        using JsonDocument metadata = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(outputDirectory, "README.test.oracle.json")));
        JsonElement metadataRoot = metadata.RootElement;
        Assert.AreEqual(root, metadataRoot.GetProperty("RuntimeRoot").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(
            metadataRoot.GetProperty("RuntimeCommit").GetString()));
    }

    /// <summary>
    /// Verifies oracle capture can retain bounded output without failing.
    /// Large disassembly tools can produce very large streams in CI.
    /// The capture metadata records truncation instead of growing memory without bound.
    /// </summary>
    [TestMethod]
    public void CaptureDisasmOracle_TruncatesLargeOutput()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "Capture-DisasmOracle.cs");
        string outputDirectory = Path.Combine(_tempRoot, "truncated-oracles");

        var (exitCode, _, _) = RunFileApp(
            root,
            scriptPath,
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

        Assert.AreEqual(0, exitCode);
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
    [TestMethod]
    public void CaptureDisasmOracle_AllowsOracleFailure()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "Capture-DisasmOracle.cs");
        string outputDirectory = Path.Combine(_tempRoot, "failure-oracles");

        var (exitCode, _, _) = RunFileApp(
            root,
            scriptPath,
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

        Assert.AreEqual(0, exitCode);
        string metadata = File.ReadAllText(Path.Combine(outputDirectory, "README.test.oracle.json"));
        Assert.Contains("\"OracleExitCode\":", metadata);
        Assert.DoesNotContain("\"OracleExitCode\": 0", metadata);
    }

    /// <summary>
    /// Verifies the repeated test runner app builds and exposes usage without running tests.
    /// Flake-hunting helpers should remain cheap to validate in normal unit tests.
    /// The real suite is exercised by CI through the script's forwarded dotnet test command.
    /// </summary>
    [TestMethod]
    public void RunTests_BuildsAndPrintsHelp()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "Run-Tests.cs");

        var (runExitCode, stdout, _) = RunFileApp(root, scriptPath, "-Help");
        Assert.AreEqual(0, runExitCode);
        Assert.Contains("-Count", stdout);
        Assert.Contains("dotnet test", stdout);
    }

    /// <summary>
    /// Verifies the Native AOT CI app builds and exposes its required inputs.
    /// Help must remain safe to invoke without publishing native applications.
    /// The test catches file-based app compilation errors before matrix jobs run.
    /// </summary>
    [TestMethod]
    public void VerifyNativeAot_BuildsAndPrintsHelp()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "Verify-NativeAot.cs");

        var (exitCode, stdout, _) = RunFileApp(root, scriptPath, "-Help");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("-Mode", stdout);
        Assert.Contains("-Rid", stdout);
        Assert.Contains("-Version", stdout);
        Assert.Contains("Native AOT", stdout);
    }

    /// <summary>
    /// Verifies the Native AOT workflows delegate their orchestration to the file-based app.
    /// Musl builds stay in the pinned Alpine SDK instead of linking on a glibc host.
    /// Installed Windows tools are checked through the command shim created by dotnet tool.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAotWorkflows_UseFileBasedAppAndCorrectBuildEnvironments()
    {
        string root = FindRepositoryRoot();
        string ciWorkflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        string releaseWorkflow = File.ReadAllText(
            Path.Combine(root, ".github", "workflows", "release.yml"));
        string script = File.ReadAllText(Path.Combine(root, "scripts", "Verify-NativeAot.cs"));
        int jobStart = ciWorkflow.IndexOf("  native-aot:", StringComparison.Ordinal);
        int jobEnd = ciWorkflow.IndexOf("  deploy-tests:", jobStart, StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, jobStart);
        Assert.IsGreaterThan(jobStart, jobEnd);
        string job = ciWorkflow[jobStart..jobEnd];
        Assert.Contains("dotnet run --file ./scripts/Verify-NativeAot.cs", job);
        Assert.Contains("-Mode CI", job);
        Assert.DoesNotContain("shell: pwsh", job);
        Assert.DoesNotContain("shell: bash", job);
        Assert.DoesNotContain("musl-tools", job);

        int releaseStepStart = releaseWorkflow.IndexOf(
            "      - name: Publish, test, and pack Native AOT applications",
            StringComparison.Ordinal);
        int releaseStepEnd = releaseWorkflow.IndexOf(
            "      - name: Upload dotsider artifact",
            releaseStepStart,
            StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, releaseStepStart);
        Assert.IsGreaterThan(releaseStepStart, releaseStepEnd);
        string releaseStep = releaseWorkflow[releaseStepStart..releaseStepEnd];
        Assert.Contains("dotnet run --file ./scripts/Verify-NativeAot.cs", releaseStep);
        Assert.Contains("-Mode Release", releaseStep);
        Assert.DoesNotContain("shell: pwsh", releaseStep);
        Assert.DoesNotContain("shell: bash", releaseStep);
        Assert.DoesNotContain("musl-tools", releaseStep);
        Assert.Contains("artifacts/native-aot-release/${{ matrix.rid }}/publish", releaseWorkflow);
        Assert.Contains("artifacts/native-aot-release/${{ matrix.rid }}/symbols", releaseWorkflow);
        Assert.Contains("artifacts/native-aot-release/${{ matrix.rid }}/packages", releaseWorkflow);
        Assert.DoesNotContain("path: ./symbols/", releaseWorkflow);

        Assert.Contains("mcr.microsoft.com/dotnet/sdk:10.0.302-alpine3.23", script);
        Assert.Contains("OperatingSystem.IsWindows() ? \".cmd\" : \"\"", script);
        Assert.Contains("runtime-tracing-target", script);
        Assert.Contains("\"--output\"", script);
    }

    /// <summary>
    /// Verifies new top-level utility and decoder test types have three-line summaries.
    /// This keeps internal helper types documented enough for editor hovers.
    /// The file list is intentionally scoped to the new file-based app and architecture work.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NewTopLevelTypes_HaveThreeLineSummaries()
    {
        string root = FindRepositoryRoot();
        string[] relativePaths =
        [
            "scripts/Capture-DisasmOracle.cs",
            "scripts/Run-Tests.cs",
            "scripts/ScriptSupport.cs",
            "scripts/Verify-NativeAot.cs",
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
            Assert.IsTrue(HasThreeLineSummaryBeforeFirstType(text), relativePath);
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
        TestProcessEnvironment.ConfigureFileApp(startInfo);

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

    private static (int ExitCode, string Stdout, string Stderr) RunFileApp(string workingDirectory, string scriptPath, params string[] arguments)
    {
        lock (s_fileAppExecutionLock)
        {
            EnsureFileAppBuilt(workingDirectory, scriptPath);

            var dotnetArguments = new List<string>
            {
                "run",
                "--file",
                scriptPath,
                "--no-build",
                "--",
            };
            dotnetArguments.AddRange(arguments);
            return RunDotnet(workingDirectory, [.. dotnetArguments]);
        }
    }

    private static void EnsureFileAppBuilt(string workingDirectory, string scriptPath)
    {
        string fullPath = Path.GetFullPath(scriptPath);
        Lazy<bool> built = s_builtFileApps.GetOrAdd(
            fullPath,
            path => new Lazy<bool>(() =>
            {
                var (exitCode, _, _) = RunDotnet(
                    workingDirectory,
                    "build",
                    path,
                    "--no-incremental",
                    "--nologo",
                    "--verbosity",
                    "quiet");
                Assert.AreEqual(0, exitCode);
                return true;
            }));
        _ = built.Value;
    }

    private static bool HasThreeLineSummaryBeforeFirstType(string text)
    {
        Match typeMatch = TopLevelTypeRegex().Match(text);
        Assert.IsTrue(typeMatch.Success, "No top-level type declaration found.");

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
