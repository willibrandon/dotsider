#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PackAsTool=false
#:package System.CommandLine@2.0.9
#:include ScriptSupport.cs

using System.CommandLine;
using System.Diagnostics;

try
{
    return TestRunApp.Run(args);
}
catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException or FileNotFoundException or InvalidOperationException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

/// <summary>
/// Runs the repository test suite through <c>dotnet test</c>.
/// The app can repeat the same command to expose parallelization flakes.
/// Extra arguments are forwarded unchanged after the script argument separator.
/// System.CommandLine provides option parsing and help output.
/// Child <c>dotnet test</c> processes disable MSBuild node reuse so repeat runs
/// do not leave idle build worker processes behind on developer machines.
/// </summary>
/// <remarks>
/// With no arguments, <c>dotnet run --file ./scripts/Run-Tests.cs</c> runs
/// <c>dotnet test</c> once for the full solution. Use <c>-Count</c> to repeat
/// the same command, <c>-Target</c> to select a project or solution, and
/// <c>--</c> to forward native <c>dotnet test</c> arguments such as
/// <c>--filter "FullyQualifiedName~SomeTest"</c>.
/// </remarks>
internal static class TestRunApp
{
    private const string DisableMsBuildNodeReuseVariable = "MSBUILDDISABLENODEREUSE";

    /// <summary>
    /// Parses script arguments and executes one or more <c>dotnet test</c> attempts.
    /// The first failing exit code is returned after all attempts finish.
    /// Use <c>-StopOnFailure</c> when an early failing attempt is enough evidence.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    internal static int Run(string[] args)
    {
        Option<int?> countOption = new("--count", "-Count")
        {
            HelpName = "N",
            Description = "Number of test attempts. Defaults to 1.",
        };
        Option<int?> repeatOption = new("--repeat", "-Repeat")
        {
            HelpName = "N",
            Description = "Alias for -Count.",
        };
        Option<string?> targetOption = new("--target", "-Target")
        {
            HelpName = "PATH",
            Description = "Optional solution, project, or directory passed to dotnet test.",
        };
        Option<string?> workingDirectoryOption = new("--working-directory", "-WorkingDirectory")
        {
            HelpName = "DIR",
            Description = "Directory for dotnet test. Defaults to the repository root.",
        };
        Option<int?> timeoutSecondsOption = new("--timeout-seconds", "-TimeoutSeconds")
        {
            HelpName = "SECONDS",
            Description = "Per-attempt process timeout in seconds.",
        };
        Option<string?> logDirectoryOption = new("--log-directory", "-LogDirectory")
        {
            HelpName = "DIR",
            Description = "Write one combined stdout/stderr log per attempt.",
        };
        Option<int> maxOutputCharactersOption = new("--max-output-characters", "-MaxOutputCharacters")
        {
            HelpName = "CHARS",
            Description = "Maximum stdout/stderr characters retained per stream when logging.",
            DefaultValueFactory = _ => 4_000_000,
        };
        Option<bool> stopOnFailureOption = new("--stop-on-failure", "-StopOnFailure")
        {
            Description = "Stop after the first failing attempt.",
        };
        Argument<string[]> dotnetTestArgument = new("dotnet-test-arguments")
        {
            HelpName = "ARGS",
            Description = "Arguments forwarded to dotnet test after --.",
            Arity = ArgumentArity.ZeroOrMore,
        };
        RootCommand rootCommand = new("Runs dotnet test once or repeatedly.")
        {
            countOption,
            repeatOption,
            targetOption,
            workingDirectoryOption,
            timeoutSecondsOption,
            logDirectoryOption,
            maxOutputCharactersOption,
            stopOnFailureOption,
            dotnetTestArgument,
        };
        rootCommand.SetAction(parseResult =>
        {
            string repositoryRoot = ScriptSupport.FindRepositoryRoot();
            string workingDirectory = ResolveWorkingDirectory(parseResult.GetValue(workingDirectoryOption), repositoryRoot);
            string target = ResolveTarget(parseResult.GetValue(targetOption), workingDirectory);
            int count = ResolveCount(parseResult.GetValue(countOption), parseResult.GetValue(repeatOption));
            TimeSpan? timeout = ResolveTimeout(parseResult.GetValue(timeoutSecondsOption));
            string logDirectory = ResolveLogDirectory(parseResult.GetValue(logDirectoryOption), workingDirectory);
            int maxOutputCharacters = ValidatePositive(parseResult.GetValue(maxOutputCharactersOption), "MaxOutputCharacters");
            bool stopOnFailure = parseResult.GetValue(stopOnFailureOption);
            string[] dotnetTestArguments = parseResult.GetValue(dotnetTestArgument) ?? [];

            return RunAttempts(
                workingDirectory,
                target,
                count,
                timeout,
                logDirectory,
                maxOutputCharacters,
                stopOnFailure,
                dotnetTestArguments);
        });

        return rootCommand.Parse(NormalizeLegacyHelpAliases(args)).Invoke();
    }

    private static int RunAttempts(
        string workingDirectory,
        string target,
        int count,
        TimeSpan? timeout,
        string logDirectory,
        int maxOutputCharacters,
        bool stopOnFailure,
        string[] dotnetTestArguments)
    {
        var commandArguments = new List<string> { "test" };
        if (!string.IsNullOrWhiteSpace(target))
        {
            commandArguments.Add(target);
        }

        commandArguments.AddRange(dotnetTestArguments);

        var failureCount = 0;
        var firstFailureExitCode = 0;
        var attemptsRun = 0;
        var total = Stopwatch.StartNew();

        for (var attempt = 1; attempt <= count; attempt++)
        {
            attemptsRun++;
            Console.WriteLine();
            Console.WriteLine($"dotnet test attempt {attempt}/{count}");
            Console.WriteLine($"dotnet {FormatCommand(commandArguments)}");

            TestAttemptResult result = string.IsNullOrWhiteSpace(logDirectory)
                ? RunDotnetTestStreaming(commandArguments, workingDirectory, timeout)
                : RunDotnetTestCaptured(commandArguments, workingDirectory, timeout, logDirectory, maxOutputCharacters, attempt);

            string outcome = result.ExitCode == 0 ? "passed" : result.TimedOut ? "timed out" : "failed";
            Console.WriteLine($"attempt {attempt}/{count} {outcome} in {result.Elapsed:c} (exit code {result.ExitCode})");
            if (!string.IsNullOrWhiteSpace(result.LogPath))
            {
                Console.WriteLine(result.LogPath);
            }

            if (result.ExitCode != 0)
            {
                failureCount++;
                firstFailureExitCode = firstFailureExitCode == 0 ? result.ExitCode : firstFailureExitCode;
                if (stopOnFailure)
                {
                    break;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"test attempts completed: {attemptsRun} requested={count} failed={failureCount} elapsed={total.Elapsed:c}");
        return failureCount == 0 ? 0 : firstFailureExitCode;
    }

    private static TestAttemptResult RunDotnetTestStreaming(
        List<string> commandArguments,
        string workingDirectory,
        TimeSpan? timeout)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        ConfigureDotnetTestEnvironment(startInfo.Environment);
        foreach (string argument in commandArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var stopwatch = Stopwatch.StartNew();
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet.");
        bool timedOut = WaitForExit(process, timeout);
        return new TestAttemptResult(timedOut ? 124 : process.ExitCode, stopwatch.Elapsed, timedOut, "");
    }

    private static TestAttemptResult RunDotnetTestCaptured(
        List<string> commandArguments,
        string workingDirectory,
        TimeSpan? timeout,
        string logDirectory,
        int maxOutputCharacters,
        int attempt)
    {
        var stopwatch = Stopwatch.StartNew();
        (int exitCode, string stdout, string stderr, bool stdoutTruncated, bool stderrTruncated, bool timedOut) = ScriptSupport.RunProcess(
            "dotnet",
            commandArguments,
            workingDirectory,
            maxOutputCharacters,
            timeout,
            CreateDotnetTestEnvironment());

        string logPath = Path.Combine(logDirectory, $"dotnet-test-{attempt:0000}.log");
        ScriptSupport.WriteTextFile(
            logPath,
            string.Join(
                Environment.NewLine,
                $"WorkingDirectory: {workingDirectory}",
                $"Command: dotnet {FormatCommand(commandArguments)}",
                $"ExitCode: {(timedOut ? 124 : exitCode)}",
                $"TimedOut: {timedOut}",
                $"StdoutTruncated: {stdoutTruncated}",
                $"StderrTruncated: {stderrTruncated}",
                "",
                "stdout:",
                stdout,
                "",
                "stderr:",
                stderr));

        Console.Write(stdout);
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Console.Error.Write(stderr);
        }

        return new TestAttemptResult(timedOut ? 124 : exitCode, stopwatch.Elapsed, timedOut, logPath);
    }

    private static Dictionary<string, string?> CreateDotnetTestEnvironment() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [DisableMsBuildNodeReuseVariable] = "1",
        };

    private static void ConfigureDotnetTestEnvironment(IDictionary<string, string?> environment)
    {
        environment[DisableMsBuildNodeReuseVariable] = "1";
    }

    private static bool WaitForExit(Process process, TimeSpan? timeout)
    {
        if (timeout is null)
        {
            process.WaitForExit();
            return false;
        }

        int milliseconds = checked((int)Math.Min(timeout.Value.TotalMilliseconds, int.MaxValue));
        if (milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
        }

        if (process.WaitForExit(milliseconds))
        {
            return false;
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        process.WaitForExit();
        return true;
    }

    private static string ResolveWorkingDirectory(string? value, string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return repositoryRoot;
        }

        return ScriptSupport.ResolveWorkingDirectory(value);
    }

    private static string ResolveTarget(string? value, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        string candidate = Path.IsPathFullyQualified(value)
            ? value
            : Path.Combine(workingDirectory, value);
        if (File.Exists(candidate) || Directory.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        throw new FileNotFoundException($"Target '{value}' does not exist.");
    }

    private static int ResolveCount(int? count, int? repeat) =>
        ValidatePositive(count ?? repeat ?? 1, "Count");

    private static TimeSpan? ResolveTimeout(int? timeoutSeconds) =>
        timeoutSeconds is null
            ? null
            : TimeSpan.FromSeconds(ValidatePositive(timeoutSeconds.Value, "TimeoutSeconds"));

    private static string ResolveLogDirectory(string? value, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        string resolved = Path.IsPathFullyQualified(value)
            ? value
            : Path.Combine(workingDirectory, value);
        Directory.CreateDirectory(resolved);
        return Path.GetFullPath(resolved);
    }

    private static int ValidatePositive(int value, string optionName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"-{optionName} must be a positive integer.");
        }

        return value;
    }

    private static string[] NormalizeLegacyHelpAliases(string[] args)
    {
        string[] normalized = [.. args];
        for (var i = 0; i < normalized.Length; i++)
        {
            if (normalized[i].Equals("--", StringComparison.Ordinal))
            {
                break;
            }

            if (IsLegacyHelpAlias(normalized[i]))
            {
                normalized[i] = "--help";
            }
        }

        return normalized;
    }

    private static bool IsLegacyHelpAlias(string token) =>
        token.Equals("-help", StringComparison.OrdinalIgnoreCase)
        || token.Equals("--help", StringComparison.OrdinalIgnoreCase)
        || token.Equals("-?", StringComparison.OrdinalIgnoreCase)
        || token.Equals("/?", StringComparison.OrdinalIgnoreCase);

    private static string FormatCommand(IEnumerable<string> arguments) =>
        string.Join(' ', arguments.Select(QuoteArgument));

    private static string QuoteArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        return argument.Any(char.IsWhiteSpace) ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : argument;
    }

    private readonly record struct TestAttemptResult(int ExitCode, TimeSpan Elapsed, bool TimedOut, string LogPath);
}
