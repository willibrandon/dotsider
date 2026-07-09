#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PackAsTool=false
#:package System.CommandLine@2.0.9
#:include ScriptSupport.cs

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
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
/// System.CommandLine provides option parsing, help, and shell completion metadata.
/// Child <c>dotnet test</c> processes disable MSBuild node reuse so repeat runs
/// do not leave idle build worker processes behind on developer machines.
/// </summary>
/// <remarks>
/// With no arguments, <c>dotnet run --file ./scripts/Run-Tests.cs</c> runs
/// <c>dotnet test</c> once for the full solution. Use <c>-Count</c> to repeat
/// the same command, <c>-Target</c> to select a project or solution, and
/// <c>--</c> to forward native <c>dotnet test</c> arguments such as
/// <c>--filter "FullyQualifiedName~SomeTest"</c>. Shell completions are exposed
/// through System.CommandLine; publish the script and register the apphost with
/// <c>dotnet-suggest</c> as described by <c>-Help</c>.
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
        targetOption.CompletionSources.Add(context => CompleteTargets(context, workingDirectoryOption));
        workingDirectoryOption.CompletionSources.Add(context => CompleteDirectories(context, workingDirectoryOption));
        logDirectoryOption.CompletionSources.Add(context => CompleteDirectories(context, workingDirectoryOption));

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
        ConfigureHelp(rootCommand);
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

    private static void ConfigureHelp(RootCommand rootCommand)
    {
        foreach (Option option in rootCommand.Options)
        {
            if (option is HelpOption { Action: HelpAction helpAction } helpOption)
            {
                helpOption.Action = new RunTestsHelpAction(helpAction);
                return;
            }
        }
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

    private sealed class RunTestsHelpAction(HelpAction defaultHelp) : SynchronousCommandLineAction
    {
        public override bool ClearsParseErrors => true;

        public override int Invoke(ParseResult parseResult)
        {
            int exitCode = defaultHelp.Invoke(parseResult);
            TextWriter output = parseResult.InvocationConfiguration.Output;
            output.WriteLine();
            output.WriteLine("Completions:");
            output.WriteLine("  System.CommandLine exposes completion metadata for this app.");
            output.WriteLine("  Publish a stable apphost, then register that executable with dotnet-suggest:");
            output.WriteLine("    dotnet publish ./scripts/Run-Tests.cs -o ./artifacts/scripts/Run-Tests");
            output.WriteLine("    dotnet tool install -g dotnet-suggest");
            output.WriteLine("    dotnet-suggest register --command-path ./artifacts/scripts/Run-Tests/Run-Tests");
            output.WriteLine("  On Windows, register ./artifacts/scripts/Run-Tests/Run-Tests.exe.");
            output.WriteLine("  Add the dotnet-suggest shim to your shell profile once; PowerShell, bash, and zsh are supported.");
            return exitCode;
        }
    }

    private static IEnumerable<string> CompleteTargets(
        CompletionContext context,
        Option<string?> workingDirectoryOption) =>
        CompleteFileSystemEntries(
            context,
            ResolveCompletionWorkingDirectory(context, workingDirectoryOption),
            includeDirectories: true,
            includeFiles: true,
            filePredicate: IsDotnetTestTargetFile);

    private static IEnumerable<string> CompleteDirectories(
        CompletionContext context,
        Option<string?> workingDirectoryOption) =>
        CompleteFileSystemEntries(
            context,
            ResolveCompletionWorkingDirectory(context, workingDirectoryOption),
            includeDirectories: true,
            includeFiles: false,
            filePredicate: static _ => false);

    private static string ResolveCompletionWorkingDirectory(
        CompletionContext context,
        Option<string?> workingDirectoryOption)
    {
        string repositoryRoot;
        try
        {
            repositoryRoot = ScriptSupport.FindRepositoryRoot();
        }
        catch (DirectoryNotFoundException)
        {
            repositoryRoot = Directory.GetCurrentDirectory();
        }

        string? value = context.ParseResult.GetValue(workingDirectoryOption);
        if (string.IsNullOrWhiteSpace(value))
        {
            return repositoryRoot;
        }

        string resolved = Path.IsPathFullyQualified(value)
            ? value
            : Path.Combine(repositoryRoot, value);
        return Directory.Exists(resolved) ? Path.GetFullPath(resolved) : repositoryRoot;
    }

    private static IEnumerable<string> CompleteFileSystemEntries(
        CompletionContext context,
        string baseDirectory,
        bool includeDirectories,
        bool includeFiles,
        Func<string, bool> filePredicate)
    {
        if (context is not TextCompletionContext)
        {
            return [];
        }

        string wordToComplete = context.WordToComplete;
        (string directoryPart, string entryPrefix) = SplitCompletionPath(wordToComplete);
        string searchDirectory = ResolveCompletionDirectory(baseDirectory, directoryPart);
        if (!Directory.Exists(searchDirectory))
        {
            return [];
        }

        return EnumerateCompletionEntries(
            searchDirectory,
            directoryPart,
            entryPrefix,
            includeDirectories,
            includeFiles,
            filePredicate);
    }

    private static IEnumerable<string> EnumerateCompletionEntries(
        string searchDirectory,
        string directoryPart,
        string entryPrefix,
        bool includeDirectories,
        bool includeFiles,
        Func<string, bool> filePredicate)
    {
        IEnumerable<string> directories = includeDirectories
            ? Directory.EnumerateDirectories(searchDirectory)
                .Where(path => Path.GetFileName(path).StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(path => directoryPart + Path.GetFileName(path) + Path.DirectorySeparatorChar)
            : [];

        IEnumerable<string> files = includeFiles
            ? Directory.EnumerateFiles(searchDirectory)
                .Where(filePredicate)
                .Where(path => Path.GetFileName(path).StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(path => directoryPart + Path.GetFileName(path))
            : [];

        return directories.Concat(files).Order(StringComparer.OrdinalIgnoreCase);
    }

    private static (string DirectoryPart, string EntryPrefix) SplitCompletionPath(string path)
    {
        int separator = path.LastIndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return separator < 0
            ? ("", path)
            : (path[..(separator + 1)], path[(separator + 1)..]);
    }

    private static string ResolveCompletionDirectory(string baseDirectory, string directoryPart)
    {
        if (string.IsNullOrWhiteSpace(directoryPart))
        {
            return baseDirectory;
        }

        return Path.IsPathFullyQualified(directoryPart)
            ? directoryPart
            : Path.Combine(baseDirectory, directoryPart);
    }

    private static bool IsDotnetTestTargetFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".fsproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".vbproj", StringComparison.OrdinalIgnoreCase);
    }
}
