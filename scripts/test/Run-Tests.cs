#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PackAsTool=false
#:include ../ScriptSupport.cs

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
/// </summary>
internal static class TestRunApp
{
    /// <summary>
    /// Parses script arguments and executes one or more <c>dotnet test</c> attempts.
    /// The first failing exit code is returned after all attempts finish.
    /// Use <c>-StopOnFailure</c> when an early failing attempt is enough evidence.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    internal static int Run(string[] args)
    {
        if (args.Any(IsHelpToken))
        {
            WriteUsage();
            return 0;
        }

        (Dictionary<string, List<string>> values, HashSet<string> switches) = ScriptSupport.ParseArguments(
            args,
            ["Count", "Repeat", "Target", "WorkingDirectory", "TimeoutSeconds", "LogDirectory", "MaxOutputCharacters"],
            ["AdditionalArguments"],
            ["StopOnFailure", "Help"]);

        if (ScriptSupport.GetSwitch(switches, "Help"))
        {
            WriteUsage();
            return 0;
        }

        string repositoryRoot = ScriptSupport.FindRepositoryRoot();
        string workingDirectory = ResolveWorkingDirectory(ScriptSupport.GetString(values, "WorkingDirectory"), repositoryRoot);
        string target = ResolveTarget(ScriptSupport.GetString(values, "Target"), workingDirectory);
        int count = ParsePositiveInt(
            GetFirstConfiguredString(values, "Count", "Repeat", "1"),
            "Count");
        TimeSpan? timeout = ParseOptionalTimeout(ScriptSupport.GetString(values, "TimeoutSeconds"));
        string logDirectory = ResolveLogDirectory(ScriptSupport.GetString(values, "LogDirectory"), workingDirectory);
        int maxOutputCharacters = ParsePositiveInt(
            ScriptSupport.GetString(values, "MaxOutputCharacters", "4000000"),
            "MaxOutputCharacters");
        bool stopOnFailure = ScriptSupport.GetSwitch(switches, "StopOnFailure");
        string[] dotnetTestArguments = ScriptSupport.GetStringArray(values, "AdditionalArguments", splitCommas: false);

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
            timeout);

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

    private static string ResolveWorkingDirectory(string value, string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return repositoryRoot;
        }

        return ScriptSupport.ResolveWorkingDirectory(value);
    }

    private static string ResolveTarget(string value, string workingDirectory)
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

    private static string ResolveLogDirectory(string value, string workingDirectory)
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

    private static int ParsePositiveInt(string value, string optionName)
    {
        if (!int.TryParse(value, out int result) || result <= 0)
        {
            throw new ArgumentException($"-{optionName} must be a positive integer.");
        }

        return result;
    }

    private static TimeSpan? ParseOptionalTimeout(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        int seconds = ParsePositiveInt(value, "TimeoutSeconds");
        return TimeSpan.FromSeconds(seconds);
    }

    private static string GetFirstConfiguredString(
        Dictionary<string, List<string>> values,
        string firstName,
        string secondName,
        string defaultValue)
    {
        string first = ScriptSupport.GetString(values, firstName);
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        string second = ScriptSupport.GetString(values, secondName);
        return string.IsNullOrWhiteSpace(second) ? defaultValue : second;
    }

    private static bool IsHelpToken(string token) =>
        token.Equals("-h", StringComparison.OrdinalIgnoreCase)
        || token.Equals("--help", StringComparison.OrdinalIgnoreCase)
        || token.Equals("-help", StringComparison.OrdinalIgnoreCase)
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

    private static void WriteUsage()
    {
        Console.WriteLine("Runs dotnet test once or repeatedly.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --file ./scripts/test/Run-Tests.cs");
        Console.WriteLine("  dotnet run --file ./scripts/test/Run-Tests.cs -- -Count 25 -- --no-restore");
        Console.WriteLine("  dotnet run --file ./scripts/test/Run-Tests.cs -- -Target tests/Dotsider.Tests/Dotsider.Tests.csproj -- --filter FullyQualifiedName~RuntimeTracerTests");
        Console.WriteLine();
        Console.WriteLine("No arguments runs dotnet test once from the repository root.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -Count <n>              Number of test attempts. Defaults to 1.");
        Console.WriteLine("  -Repeat <n>             Alias for -Count.");
        Console.WriteLine("  -Target <path>          Optional solution, project, or directory passed to dotnet test.");
        Console.WriteLine("  -WorkingDirectory <dir> Directory for dotnet test. Defaults to the repository root.");
        Console.WriteLine("  -TimeoutSeconds <n>     Per-attempt process timeout.");
        Console.WriteLine("  -LogDirectory <dir>     Write one combined stdout/stderr log per attempt.");
        Console.WriteLine("  -StopOnFailure          Stop after the first failing attempt.");
        Console.WriteLine("  -- <args>               Arguments forwarded to dotnet test.");
    }

    private readonly record struct TestAttemptResult(int ExitCode, TimeSpan Elapsed, bool TimedOut, string LogPath);
}
