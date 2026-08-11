#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PackAsTool=false
#:include ScriptSupport.cs

using System.Diagnostics;
using System.Xml.Linq;

const string cliPackageId = "Hex1b.Tool";

if (args.Length == 1 && IsHelpArgument(args[0]))
{
    Console.WriteLine("Initializes the dotsider development container.");
    Console.WriteLine("Restores repository dependencies and installs Hex1b.Tool.");
    return 0;
}

if (args.Length != 0)
{
    Console.Error.WriteLine($"Unexpected argument '{args[0]}'. Use --help for usage.");
    return 2;
}

if (!OperatingSystem.IsLinux()
    || !string.Equals(
        Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        "true",
        StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("This utility must run inside the dotsider development container.");
    return 1;
}

try
{
    string repositoryRoot = ScriptSupport.FindRepositoryRoot();
    string hex1bVersion = ReadHex1bVersion(repositoryRoot);

    ConfigureGitSafeDirectory(repositoryRoot);
    InstallOrUpdateGlobalTool(repositoryRoot, cliPackageId, hex1bVersion);

    RunCommand("dotnet", ["restore", "Dotsider.slnx"], repositoryRoot);
    RestoreFileApps(repositoryRoot);
    RunCommand(
        "pnpm",
        ["install", "--frozen-lockfile"],
        Path.Combine(repositoryRoot, "docs"),
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CI"] = "true",
        });
    string[] pnpmPackageDirectories =
    [
        "integrations/size-check",
        "azure-devops",
    ];
    foreach (string packageDirectory in pnpmPackageDirectories)
    {
        RunCommand(
            "pnpm",
            ["install", "--frozen-lockfile"],
            Path.Combine(repositoryRoot, packageDirectory),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CI"] = "true",
            });
    }

    VerifyCommand("dotnet", ["--version"], repositoryRoot);
    VerifyCommand("node", ["--version"], repositoryRoot);
    VerifyCommand("pnpm", ["--version"], repositoryRoot);
    VerifyCommand("clang", ["--version"], repositoryRoot);
    VerifyCommand("file", ["--version"], repositoryRoot);
    VerifyCommand("llvm-objdump", ["--version"], repositoryRoot);
    VerifyCommand("docker", ["version", "--format", "{{.Server.Version}}"], repositoryRoot);
    VerifyGlobalToolVersion(ReadGlobalTools(repositoryRoot), cliPackageId, hex1bVersion);

    Console.WriteLine();
    Console.WriteLine("Development container initialization completed.");
    return 0;
}
catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static bool IsHelpArgument(string argument)
{
    return argument is "--help" or "-h" or "-Help" or "-?";
}

static void ConfigureGitSafeDirectory(string repositoryRoot)
{
    (int exitCode, string stdout, string stderr, _, _, _) = ScriptSupport.RunProcess(
        "git",
        ["config", "--global", "--get-all", "safe.directory"],
        repositoryRoot);
    if (exitCode is not 0 and not 1)
    {
        throw new InvalidOperationException(
            $"git config failed with exit code {exitCode}.{Environment.NewLine}{stderr}");
    }

    bool isConfigured = stdout
        .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
        .Contains(repositoryRoot, StringComparer.Ordinal);
    if (!isConfigured)
    {
        RunCommand(
            "git",
            ["config", "--global", "--add", "safe.directory", repositoryRoot],
            repositoryRoot);
    }
}

static string ReadHex1bVersion(string repositoryRoot)
{
    string packageFile = Path.Combine(repositoryRoot, "Directory.Packages.props");
    XDocument document = XDocument.Load(packageFile, LoadOptions.None);
    XElement? packageVersion = document
        .Descendants()
        .SingleOrDefault(element =>
            element.Name.LocalName == "PackageVersion"
            && string.Equals(
                element.Attribute("Include")?.Value,
                "Hex1b",
                StringComparison.OrdinalIgnoreCase));
    string? version = packageVersion?.Attribute("Version")?.Value;
    return !string.IsNullOrWhiteSpace(version)
        ? version
        : throw new InvalidOperationException($"Hex1b does not have a version in '{packageFile}'.");
}

static void RestoreFileApps(string repositoryRoot)
{
    string[] fileApps =
    [
        "scripts/Capture-DisasmOracle.cs",
        "scripts/Deploy-Website.cs",
        "scripts/Run-Tests.cs",
        "scripts/Validate-CiIntegrations.cs",
        "scripts/Verify-NativeAot.cs",
    ];
    foreach (string fileApp in fileApps)
    {
        RunCommand(
            "dotnet",
            ["restore", fileApp, "--nologo", "--verbosity", "quiet"],
            repositoryRoot);
    }
}

static void InstallOrUpdateGlobalTool(string repositoryRoot, string packageId, string version)
{
    Dictionary<string, string> installedTools = ReadGlobalTools(repositoryRoot);
    string verb = installedTools.ContainsKey(packageId) ? "update" : "install";
    var arguments = new List<string>
    {
        "tool",
        verb,
        "--global",
        packageId,
        "--version",
        version,
    };
    if (verb == "update")
    {
        arguments.Add("--allow-downgrade");
    }

    RunCommand("dotnet", arguments, repositoryRoot);
}

static void VerifyGlobalToolVersion(
    IReadOnlyDictionary<string, string> installedTools,
    string packageId,
    string expectedVersion)
{
    if (!installedTools.TryGetValue(packageId, out string? actualVersion)
        || !string.Equals(actualVersion, expectedVersion, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Expected global tool {packageId} {expectedVersion}, but found {actualVersion ?? "nothing"}.");
    }
}

static Dictionary<string, string> ReadGlobalTools(string repositoryRoot)
{
    (int exitCode, string stdout, string stderr, _, _, _) = ScriptSupport.RunProcess(
        "dotnet",
        ["tool", "list", "--global"],
        repositoryRoot);
    if (exitCode != 0)
    {
        throw new InvalidOperationException(
            $"dotnet tool list --global failed with exit code {exitCode}.{Environment.NewLine}{stderr}");
    }

    var tools = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (string line in stdout.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
    {
        string[] columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (columns.Length >= 2 && Version.TryParse(columns[1], out _))
        {
            tools[columns[0]] = columns[1];
        }
    }

    return tools;
}

static void VerifyCommand(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
{
    Console.WriteLine();
    Console.WriteLine($"Verifying {fileName}...");
    RunCommand(fileName, arguments, workingDirectory);
}

static void RunCommand(
    string fileName,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    IReadOnlyDictionary<string, string>? environment = null)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        UseShellExecute = false,
        WorkingDirectory = workingDirectory,
    };
    foreach (string argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    if (environment is not null)
    {
        foreach ((string name, string value) in environment)
        {
            startInfo.Environment[name] = value;
        }
    }

    using Process process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"Failed to start {fileName}.");
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"{fileName} exited with code {process.ExitCode} in '{workingDirectory}'.");
    }
}
