#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PackAsTool=false
#:include ScriptSupport.cs

using System.Text.Json.Nodes;

try
{
    return CaptureDisasmOracleApp.Run(args);
}
catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or FileNotFoundException or InvalidOperationException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

/// <summary>
/// Captures external native-disassembly oracle output and metadata.
/// The app records stable stdout, stderr, fixture hash, SDK version, and runtime clone pins.
/// Decoder work uses those captures as reviewable references, not product dependencies.
/// </summary>
internal static class CaptureDisasmOracleApp
{
    /// <summary>
    /// Runs the native-disassembly oracle capture app.
    /// Arguments use the repository script option parser and forward trailing values to the oracle.
    /// The return code mirrors the oracle unless failures are explicitly allowed.
    /// Large oracle streams are retained up to the configured output limit.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    internal static int Run(string[] args)
    {
        (Dictionary<string, List<string>> values, HashSet<string> switches) = ScriptSupport.ParseArguments(
            args,
            ["Architecture", "Fixture", "OraclePath", "OutputDirectory", "RuntimeRoot", "WorkingDirectory", "MaxOutputCharacters"],
            ["AdditionalArguments"],
            ["AllowOracleFailure"]);

        string repositoryRoot = ScriptSupport.FindRepositoryRoot();
        string architecture = ScriptSupport.GetString(values, "Architecture");
        string fixture = ScriptSupport.GetString(values, "Fixture");
        string oraclePath = ScriptSupport.GetString(values, "OraclePath");
        string outputDirectory = ScriptSupport.GetString(values, "OutputDirectory", Path.Combine(repositoryRoot, "artifacts", "oracles", "disasm"));
        string runtimeRoot = ScriptSupport.GetString(
            values,
            "RuntimeRoot",
            Environment.GetEnvironmentVariable("DOTSIDER_RUNTIME_ROOT") ?? Path.Combine(Directory.GetParent(repositoryRoot)?.FullName ?? repositoryRoot, "runtime"));
        string workingDirectory = ScriptSupport.GetString(values, "WorkingDirectory");
        int maxOutputCharacters = ParsePositiveInt(
            ScriptSupport.GetString(values, "MaxOutputCharacters", "4000000"),
            "MaxOutputCharacters");
        string[] oracleArguments = ScriptSupport.GetStringArray(values, "AdditionalArguments", splitCommas: false);
        bool allowOracleFailure = ScriptSupport.GetSwitch(switches, "AllowOracleFailure");

        if (string.IsNullOrWhiteSpace(architecture))
        {
            throw new ArgumentException("-Architecture is required.");
        }

        if (string.IsNullOrWhiteSpace(oraclePath))
        {
            throw new ArgumentException("-OraclePath is required.");
        }

        string resolvedWorkingDirectory = ScriptSupport.ResolveWorkingDirectory(workingDirectory);
        string resolvedFixture = ScriptSupport.ResolveExistingPath(fixture, "fixture", resolvedWorkingDirectory);
        string resolvedOraclePath = ScriptSupport.ResolveCommandPath(oraclePath, "disassembly oracle");
        string resolvedOutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(resolvedOutputDirectory);

        string safeStem = MakeSafeFileStem(Path.GetFileNameWithoutExtension(resolvedFixture));
        string safeArchitecture = MakeSafeFileStem(architecture);
        string outputStem = $"{safeStem}.{safeArchitecture}.oracle";
        string stdoutPath = Path.Combine(resolvedOutputDirectory, $"{outputStem}.txt");
        string stderrPath = Path.Combine(resolvedOutputDirectory, $"{outputStem}.stderr.txt");
        string metadataPath = Path.Combine(resolvedOutputDirectory, $"{outputStem}.json");

        (int exitCode, string stdout, string stderr, bool stdoutTruncated, bool stderrTruncated) = ScriptSupport.RunProcess(
            resolvedOraclePath,
            oracleArguments,
            resolvedWorkingDirectory,
            maxOutputCharacters);
        ScriptSupport.WriteTextFile(stdoutPath, NormalizeText(stdout));
        ScriptSupport.WriteTextFile(stderrPath, NormalizeText(stderr));

        string dotnetVersion = GetDotnetVersion();
        var metadata = new JsonObject
        {
            ["Tool"] = "native-disassembly",
            ["Architecture"] = architecture,
            ["Fixture"] = resolvedFixture,
            ["FixtureSha256"] = ScriptSupport.GetFileSha256(resolvedFixture),
            ["OraclePath"] = resolvedOraclePath,
            ["OracleArguments"] = ScriptSupport.ToJsonArray(oracleArguments),
            ["OracleExitCode"] = exitCode,
            ["MaxOutputCharacters"] = maxOutputCharacters,
            ["StdoutTruncated"] = stdoutTruncated,
            ["StderrTruncated"] = stderrTruncated,
            ["StdoutPath"] = stdoutPath,
            ["StderrPath"] = stderrPath,
            ["RuntimeRoot"] = Directory.Exists(runtimeRoot) ? Path.GetFullPath(runtimeRoot) : runtimeRoot,
            ["RuntimeCommit"] = ScriptSupport.TryRunGit(runtimeRoot, "rev-parse", "HEAD"),
            ["RuntimeBranch"] = ScriptSupport.TryRunGit(runtimeRoot, "branch", "--show-current"),
            ["Dotnet"] = dotnetVersion,
            ["CapturedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
        };
        ScriptSupport.WriteJsonFile(metadataPath, metadata);

        if (exitCode != 0 && !allowOracleFailure)
        {
            throw new InvalidOperationException($"Disassembly oracle exited with code {exitCode}. See '{stderrPath}'.");
        }

        Console.Out.WriteLine(stdoutPath);
        Console.Out.WriteLine(metadataPath);
        return allowOracleFailure ? 0 : exitCode;
    }

    /// <summary>
    /// Parses a positive integer script option.
    /// The capture app uses this for bounded oracle output retention.
    /// Invalid values fail early with the option name in the diagnostic.
    /// </summary>
    /// <param name="value">The option value.</param>
    /// <param name="optionName">The option name.</param>
    /// <returns>The parsed positive integer.</returns>
    private static int ParsePositiveInt(string value, string optionName)
    {
        if (!int.TryParse(value, out int result) || result <= 0)
        {
            throw new ArgumentException($"-{optionName} must be a positive integer.");
        }

        return result;
    }

    /// <summary>
    /// Normalizes process output for stable oracle captures.
    /// </summary>
    /// <param name="text">The process output text.</param>
    /// <returns>The normalized output text.</returns>
    private static string NormalizeText(string text)
    {
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        return string.Join(Environment.NewLine, lines.Select(static line => line.TrimEnd())) + Environment.NewLine;
    }

    /// <summary>
    /// Converts arbitrary text into a safe file-name stem.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <returns>The safe file-name stem.</returns>
    private static string MakeSafeFileStem(string value)
    {
        char[] chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Path.GetInvalidFileNameChars().Contains(chars[i]) || chars[i] is '/' or '\\')
            {
                chars[i] = '-';
            }
        }

        return new string(chars);
    }

    /// <summary>
    /// Captures the active .NET SDK version.
    /// </summary>
    /// <returns>The SDK version, or an empty string on failure.</returns>
    private static string GetDotnetVersion()
    {
        (int exitCode, string stdout, _, _, _) = ScriptSupport.RunProcess("dotnet", ["--version"], Directory.GetCurrentDirectory());
        return exitCode == 0 ? stdout.Trim() : string.Empty;
    }
}
