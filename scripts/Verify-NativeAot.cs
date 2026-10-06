#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PackAsTool=false
#:include ScriptSupport.cs
#:include NativeAotPayloadValidator.cs

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

try
{
    return NativeAotVerificationApp.Run(args);
}
catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException or JsonException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

/// <summary>
/// Publishes and verifies the dotsider Native AOT applications and tool packages.
/// Musl targets build and run inside the pinned Alpine SDK container.
/// CI and release workflows share this app so their package checks cannot drift.
/// </summary>
internal static class NativeAotVerificationApp
{
    private const string AlpineSdkImage = "mcr.microsoft.com/dotnet/sdk:10.0.302-alpine3.23";
    private const string CiMode = "ci";
    private const string ReleaseMode = "release";

    private static readonly string[] s_supportedRids =
    [
        "win-x64",
        "win-arm64",
        "linux-x64",
        "linux-arm64",
        "linux-musl-x64",
        "linux-musl-arm64",
        "osx-x64",
        "osx-arm64",
    ];

    private static readonly (string Name, string Project, string PackageId)[] s_products =
    [
        ("dotsider", "src/Dotsider/Dotsider.csproj", "Dotsider"),
        ("dotsider-mcp", "src/Dotsider.Mcp/Dotsider.Mcp.csproj", "Dotsider.Mcp"),
    ];

    /// <summary>
    /// Parses the verification options and selects the matching native build environment.
    /// The returned exit code is suitable for direct use by GitHub Actions.
    /// Help exits without restoring, publishing, or changing artifacts.
    /// </summary>
    /// <param name="args">The file-based app arguments.</param>
    /// <returns>The process exit code.</returns>
    internal static int Run(string[] args)
    {
        if (args.Any(IsHelpArgument))
        {
            PrintHelp();
            return 0;
        }

        (Dictionary<string, List<string>> values, HashSet<string> switches) = ScriptSupport.ParseArguments(
            args,
            ["Mode", "Rid", "Version"],
            [],
            ["InsideMuslContainer"]);

        string mode = RequireValue(values, "Mode").ToLowerInvariant();
        if (mode is not CiMode and not ReleaseMode)
        {
            throw new ArgumentException("-Mode must be either CI or Release.", nameof(args));
        }

        string rid = RequireValue(values, "Rid").ToLowerInvariant();
        if (!s_supportedRids.Contains(rid, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported runtime identifier '{rid}'.", nameof(args));
        }

        string version = NormalizeVersion(RequireValue(values, "Version"));
        bool insideMuslContainer = ScriptSupport.GetSwitch(switches, "InsideMuslContainer");
        string repositoryRoot = ScriptSupport.FindRepositoryRoot();

        if (rid.StartsWith("linux-musl-", StringComparison.Ordinal) && !insideMuslContainer)
        {
            return RunInMuslContainer(repositoryRoot, mode, rid, version);
        }

        ValidateBuildHost(rid, insideMuslContainer);
        if (rid.StartsWith("linux-", StringComparison.Ordinal) && !insideMuslContainer)
        {
            InstallLinuxPrerequisites(repositoryRoot);
        }

        VerifyNativeAot(repositoryRoot, mode, rid, version);
        return 0;
    }

    private static int RunInMuslContainer(
        string repositoryRoot,
        string mode,
        string rid,
        string version)
    {
        string docker = ScriptSupport.ResolveCommandPath("docker", "Docker CLI");
        string containerName = $"dotsider-native-aot-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var created = false;

        try
        {
            RunChecked(docker, ["pull", AlpineSdkImage], repositoryRoot);
            RunChecked(
                docker,
                [
                    "create",
                    "--name",
                    containerName,
                    "--volume",
                    $"{repositoryRoot}:/work",
                    "--workdir",
                    "/work",
                    AlpineSdkImage,
                    "tail",
                    "-f",
                    "/dev/null",
                ],
                repositoryRoot);
            created = true;
            RunChecked(docker, ["start", containerName], repositoryRoot);
            RunChecked(
                docker,
                ["exec", containerName, "apk", "add", "--no-cache", "clang", "build-base", "zlib-dev"],
                repositoryRoot);
            RunChecked(
                docker,
                [
                    "exec",
                    containerName,
                    "dotnet",
                    "run",
                    "--file",
                    "./scripts/Verify-NativeAot.cs",
                    "--",
                    "-Mode",
                    mode,
                    "-Rid",
                    rid,
                    "-Version",
                    version,
                    "-InsideMuslContainer",
                ],
                repositoryRoot);
            return 0;
        }
        finally
        {
            if (created)
            {
                TryRun(docker, ["rm", "--force", containerName], repositoryRoot);
            }
        }
    }

    private static void VerifyNativeAot(
        string repositoryRoot,
        string mode,
        string rid,
        string version)
    {
        bool release = mode.Equals(ReleaseMode, StringComparison.Ordinal);
        string executableExtension = rid.StartsWith("win-", StringComparison.Ordinal) ? ".exe" : "";
        string outputRoot = release
            ? Path.Join(repositoryRoot, "artifacts", "native-aot-release", rid)
            : Path.Join(repositoryRoot, "artifacts", "native-aot", rid);
        string nativeRoot = release ? Path.Join(outputRoot, "publish") : outputRoot;
        string symbolRoot = Path.Join(outputRoot, "symbols");
        string packageRoot = Path.Join(outputRoot, "packages");
        string smokeOutput = Path.Join(outputRoot, "runtime-tracing");
        string traceTargetOutput = Path.Join(outputRoot, "runtime-tracing-target");

        RecreateDirectory(repositoryRoot, outputRoot);
        Directory.CreateDirectory(nativeRoot);
        Directory.CreateDirectory(symbolRoot);
        Directory.CreateDirectory(packageRoot);
        Directory.CreateDirectory(smokeOutput);
        Directory.CreateDirectory(traceTargetOutput);

        RunDotnetChecked(
            repositoryRoot,
            [
                "build",
                "samples/HelloWorld/HelloWorld.csproj",
                "--configuration",
                "Release",
                "--output",
                traceTargetOutput,
            ]);

        foreach ((string name, string project, string packageId) in s_products)
        {
            string output = Path.Join(nativeRoot, name);
            Directory.CreateDirectory(output);
            RunDotnetChecked(
                repositoryRoot,
                [
                    "publish",
                    project,
                    "--configuration",
                    "Release",
                    "--runtime",
                    rid,
                    $"-p:Version={version}",
                    $"-p:PackageVersion={version}",
                    "--output",
                    output,
                ]);

            string executable = Path.Join(output, name + executableExtension);
            RequireFile(executable, $"Missing Native AOT executable for {name}.");
            ValidateTraceHost(output, name);
            RunChecked(executable, ["--version"], repositoryRoot);

            MoveNativeSymbols(
                repositoryRoot,
                output,
                Path.Join(symbolRoot, name),
                name,
                rid);
            File.Copy(
                Path.Join(repositoryRoot, "LICENSE"),
                Path.Join(output, "LICENSE"));
            NativeAotPayloadValidator.Validate(output, name, rid);

            RunDotnetChecked(
                repositoryRoot,
                [
                    "pack",
                    project,
                    "--configuration",
                    "Release",
                    "--runtime",
                    rid,
                    $"-p:Version={version}",
                    $"-p:PackageVersion={version}",
                    "--output",
                    packageRoot,
                ]);

            string package = Path.Join(packageRoot, $"{packageId}.{rid}.{version}.nupkg");
            RequireFile(package, $"Missing tool package for {packageId}.{rid}.");
            ValidateToolPackage(package, name, executableExtension, rid);
            if (!release)
            {
                InstallAndRunTool(
                    repositoryRoot,
                    packageRoot,
                    Path.Join(nativeRoot, name + "-tool"),
                    $"{packageId}.{rid}",
                    name,
                    version);
            }
        }

        RunDotnetChecked(
            repositoryRoot,
            [
                "publish",
                "tests/Dotsider.NativeAotSmoke/Dotsider.NativeAotSmoke.csproj",
                "--configuration",
                "Release",
                "--runtime",
                rid,
                "--output",
                smokeOutput,
            ]);

        string traceTarget = Path.Join(traceTargetOutput, "HelloWorld.dll");
        string smokeExecutable = Path.Join(
            smokeOutput,
            "Dotsider.NativeAotSmoke" + executableExtension);
        RequireFile(smokeExecutable, "Missing Native AOT runtime tracing smoke executable.");
        RequireFile(traceTarget, "Missing runtime tracing fixture.");
        RunChecked(smokeExecutable, [traceTarget], repositoryRoot);

        string dotsiderExecutable = Path.Join(nativeRoot, "dotsider", "dotsider" + executableExtension);
        string json = RunCapturedChecked(
            dotsiderExecutable,
            ["analyze", dotsiderExecutable, "--json"],
            repositoryRoot);
        using JsonDocument document = JsonDocument.Parse(json);
        _ = document.RootElement.ValueKind;
    }

    private static void InstallAndRunTool(
        string repositoryRoot,
        string packageRoot,
        string toolPath,
        string packageId,
        string commandName,
        string version)
    {
        Directory.CreateDirectory(toolPath);
        RunDotnetChecked(
            repositoryRoot,
            [
                "tool",
                "install",
                packageId,
                "--tool-path",
                toolPath,
                "--version",
                version,
                "--add-source",
                packageRoot,
                "--no-cache",
                "--ignore-failed-sources",
            ]);

        string shimExtension = OperatingSystem.IsWindows() ? ".cmd" : "";
        string shim = Path.Join(toolPath, commandName + shimExtension);
        RequireFile(shim, $"The installed {packageId} tool shim is missing.");
        if (OperatingSystem.IsWindows())
        {
            RunShellAssociatedChecked(shim, ["--version"], repositoryRoot);
            return;
        }

        RunChecked(shim, ["--version"], repositoryRoot);
    }

    private static void ValidateTraceHost(string output, string productName)
    {
        string traceHost = Path.Join(output, "tracehost", "dotsider-tracehost.dll");
        if (productName.Equals("dotsider", StringComparison.Ordinal))
        {
            RequireFile(traceHost, "The dotsider Native AOT payload does not contain its runtime trace host.");
            return;
        }

        if (File.Exists(traceHost))
        {
            throw new InvalidOperationException(
                "The dotsider-mcp Native AOT payload contains an unused runtime trace host.");
        }
    }

    private static void ValidateToolPackage(
        string package,
        string productName,
        string executableExtension,
        string rid)
    {
        using ZipArchive archive = ZipFile.OpenRead(package);
        string[] entryNames = [.. archive.Entries.Select(static entry => entry.FullName)];
        string[] forbidden = [.. entryNames.Where(IsForbiddenPackageEntry)];
        if (forbidden.Length != 0)
        {
            throw new InvalidOperationException(
                $"Tool package {package} contains forbidden files: {string.Join(", ", forbidden)}");
        }

        string executableSuffix = "/" + productName + executableExtension;
        if (!entryNames.Any(name =>
                name.EndsWith(executableSuffix, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Tool package {package} does not contain its Native AOT executable.");
        }

        if (productName.Equals("dotsider", StringComparison.Ordinal))
        {
            string[] missing = [.. NativeAotPayloadValidator.RequiredTerminalFiles(rid)
                .Where(file => !entryNames.Any(name => name.EndsWith("/" + file, StringComparison.OrdinalIgnoreCase)))];
            if (missing.Length != 0)
                throw new InvalidOperationException(
                    $"Tool package {package} is missing native terminal files: {string.Join(", ", missing)}");
        }

        bool containsTraceHost = entryNames.Any(name =>
            name.EndsWith(
                "/tracehost/dotsider-tracehost.dll",
                StringComparison.OrdinalIgnoreCase));
        if (productName.Equals("dotsider", StringComparison.Ordinal) && !containsTraceHost)
        {
            throw new InvalidOperationException(
                $"Tool package {package} does not contain the runtime trace host.");
        }

        if (productName.Equals("dotsider-mcp", StringComparison.Ordinal) && containsTraceHost)
        {
            throw new InvalidOperationException(
                $"Tool package {package} contains an unused runtime trace host.");
        }
    }

    private static bool IsForbiddenPackageEntry(string entryName)
    {
        string normalized = entryName.Replace('\\', '/');
        if (normalized.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".dbg", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".dwarf", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(".dSYM/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("hex1bpty", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Dia2Lib", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("TraceRelogger", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("KernelTraceControl", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("msdia", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !normalized.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
            && !normalized.EndsWith(
                "/DotnetToolSettings.xml",
                StringComparison.OrdinalIgnoreCase);
    }

    private static void MoveNativeSymbols(
        string repositoryRoot,
        string output,
        string destination,
        string productName,
        string rid)
    {
        RecreateDirectory(repositoryRoot, destination);
        foreach (string sourceDirectory in Directory.EnumerateDirectories(
                     output,
                     "*.dSYM",
                     SearchOption.TopDirectoryOnly))
        {
            Directory.Move(
                sourceDirectory,
                Path.Join(destination, Path.GetFileName(sourceDirectory)));
        }

        string[] symbols =
        [
            .. Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
                .Where(static path => IsNativeSymbolExtension(Path.GetExtension(path))),
        ];
        foreach (string symbol in symbols)
        {
            File.Move(symbol, Path.Join(destination, Path.GetFileName(symbol)));
        }

        if (!Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).Any())
        {
            throw new InvalidOperationException(
                $"No native symbols were produced for {productName} on {rid}.");
        }
    }

    private static bool IsNativeSymbolExtension(string extension) =>
        extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".dbg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".dwarf", StringComparison.OrdinalIgnoreCase);

    private static void ValidateBuildHost(string rid, bool insideMuslContainer)
    {
        bool expectedOperatingSystem = rid.StartsWith("win-", StringComparison.Ordinal)
            ? OperatingSystem.IsWindows()
            : rid.StartsWith("osx-", StringComparison.Ordinal)
                ? OperatingSystem.IsMacOS()
                : OperatingSystem.IsLinux();
        if (!expectedOperatingSystem)
        {
            throw new InvalidOperationException(
                $"The current operating system cannot produce or execute {rid}.");
        }

        Architecture expectedArchitecture = rid.EndsWith("-arm64", StringComparison.Ordinal)
            ? Architecture.Arm64
            : Architecture.X64;
        if (RuntimeInformation.ProcessArchitecture != expectedArchitecture)
        {
            throw new InvalidOperationException(
                $"The {rid} job requires a native {expectedArchitecture} host, but this process is "
                + $"{RuntimeInformation.ProcessArchitecture}.");
        }

        if (insideMuslContainer && !File.Exists("/etc/alpine-release"))
        {
            throw new InvalidOperationException(
                "Musl builds must run inside the pinned Alpine SDK container.");
        }
    }

    private static void InstallLinuxPrerequisites(string repositoryRoot)
    {
        RunChecked(
            "sudo",
            ["apt-get", "-o", "Acquire::Retries=5", "update"],
            repositoryRoot);
        RunChecked(
            "sudo",
            ["apt-get", "-o", "Acquire::Retries=5", "install", "-y", "clang", "zlib1g-dev"],
            repositoryRoot);
    }

    private static void RunDotnetChecked(string workingDirectory, IReadOnlyList<string> arguments)
    {
        RunChecked("dotnet", arguments, workingDirectory);
    }

    private static void RunChecked(
        string filePath,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        (int exitCode, string stdout, string stderr, _, _, _) = ScriptSupport.RunProcess(
            filePath,
            arguments,
            workingDirectory,
            8_000_000,
            environment: CreateProcessEnvironment());
        WriteProcessOutput(stdout, stderr);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"{FormatCommand(filePath, arguments)} failed with exit code {exitCode}.");
        }
    }

    private static string RunCapturedChecked(
        string filePath,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        (int exitCode, string stdout, string stderr, _, _, _) = ScriptSupport.RunProcess(
            filePath,
            arguments,
            workingDirectory,
            8_000_000,
            environment: CreateProcessEnvironment());
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Console.Error.Write(stderr);
        }

        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"{FormatCommand(filePath, arguments)} failed with exit code {exitCode}.");
        }

        return stdout;
    }

    private static void RunShellAssociatedChecked(
        string filePath,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(filePath)
        {
            UseShellExecute = true,
            WorkingDirectory = workingDirectory,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{filePath}'.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{FormatCommand(filePath, arguments)} failed with exit code {process.ExitCode}.");
        }
    }

    private static void TryRun(
        string filePath,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        try
        {
            (int exitCode, string stdout, string stderr, _, _, _) = ScriptSupport.RunProcess(
                filePath,
                arguments,
                workingDirectory,
                1_000_000);
            WriteProcessOutput(stdout, stderr);
            if (exitCode != 0)
            {
                Console.Error.WriteLine(
                    $"Container cleanup exited with code {exitCode}: {FormatCommand(filePath, arguments)}");
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Container cleanup failed: {ex.Message}");
        }
    }

    private static Dictionary<string, string?> CreateProcessEnvironment() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MSBUILDDISABLENODEREUSE"] = "1",
        };

    private static void WriteProcessOutput(string stdout, string stderr)
    {
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Console.Write(stdout);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Console.Error.Write(stderr);
        }
    }

    private static void RecreateDirectory(string repositoryRoot, string path)
    {
        string fullRepositoryRoot = Path.GetFullPath(repositoryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRepositoryRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to replace directory outside the repository: {fullPath}");
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }

        Directory.CreateDirectory(fullPath);
    }

    private static void RequireFile(string path, string message)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(message, path);
        }
    }

    private static string RequireValue(Dictionary<string, List<string>> values, string name)
    {
        string value = ScriptSupport.GetString(values, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"-{name} is required.", name);
        }

        return value.Trim();
    }

    private static string NormalizeVersion(string value)
    {
        string version = value.StartsWith('v') ? value[1..] : value;
        if (string.IsNullOrWhiteSpace(version) || version.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("-Version must be a valid package version.", nameof(value));
        }

        return version;
    }

    private static bool IsHelpArgument(string argument) =>
        argument.Equals("-Help", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("--help", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("-?", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("/?", StringComparison.OrdinalIgnoreCase);

    private static void PrintHelp()
    {
        Console.WriteLine("Publishes and verifies dotsider Native AOT applications and tool packages.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine(
            "  dotnet run --file ./scripts/Verify-NativeAot.cs -- "
            + "-Mode <CI|Release> -Rid <RID> -Version <VERSION>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -Mode     Select CI verification or release artifact output.");
        Console.WriteLine("  -Rid      Native runtime identifier to publish and verify.");
        Console.WriteLine("  -Version  Application and package version.");
        Console.WriteLine("  -Help     Show this help text without building.");
    }

    private static string FormatCommand(string filePath, IEnumerable<string> arguments) =>
        filePath + " " + string.Join(' ', arguments.Select(QuoteArgument));

    private static string QuoteArgument(string argument) =>
        argument.Any(char.IsWhiteSpace)
            ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : argument;
}
