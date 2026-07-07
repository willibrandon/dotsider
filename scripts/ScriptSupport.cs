using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Provides shared helpers for dotsider file-based utility apps.
/// The helpers keep command-line parsing, path resolution, process execution, and JSON output consistent.
/// File-based apps include this file so editor hovers and tests cover the reusable behavior.
/// </summary>
internal static class ScriptSupport
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly UTF8Encoding s_utf8NoBom = new(false);

    /// <summary>
    /// Parses PowerShell-style command-line options.
    /// Options may use dash, double-dash, inline equals, or separated values.
    /// The parser also treats trailing values after <c>--</c> as additional arguments.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="valueOptions">Option names that accept one value.</param>
    /// <param name="arrayOptions">Option names that accept multiple values.</param>
    /// <param name="switchOptions">Option names that behave as switches.</param>
    /// <returns>The parsed option values and switches.</returns>
    internal static (Dictionary<string, List<string>> Values, HashSet<string> Switches) ParseArguments(
        string[] args,
        string[] valueOptions,
        string[] arrayOptions,
        string[] switchOptions)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var switches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var valueOptionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arrayOptionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var switchOptionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string option in valueOptions)
        {
            string key = NormalizeOptionName(option);
            valueOptionSet.Add(key);
            knownOptions.Add(key);
        }

        foreach (string option in arrayOptions)
        {
            string key = NormalizeOptionName(option);
            arrayOptionSet.Add(key);
            knownOptions.Add(key);
        }

        foreach (string option in switchOptions)
        {
            string key = NormalizeOptionName(option);
            switchOptionSet.Add(key);
            knownOptions.Add(key);
        }

        for (var i = 0; i < args.Length; i++)
        {
            string token = args[i];
            if (token.Equals("--", StringComparison.Ordinal))
            {
                string additionalArguments = NormalizeOptionName("AdditionalArguments");
                while (++i < args.Length)
                {
                    AddValue(values, additionalArguments, args[i]);
                }

                break;
            }

            if (!TryParseOptionToken(token, knownOptions, out string name, out string? inlineValue))
            {
                throw new ArgumentException($"Unexpected argument '{token}'.");
            }

            if (switchOptionSet.Contains(name))
            {
                if (inlineValue is null || ParseBoolean(inlineValue, token))
                {
                    switches.Add(name);
                }

                continue;
            }

            if (valueOptionSet.Contains(name))
            {
                string value = inlineValue ?? ReadRequiredValue(args, ref i, token);
                AddValue(values, name, value);
                continue;
            }

            if (!arrayOptionSet.Contains(name))
            {
                throw new ArgumentException($"Unsupported argument '{token}'.");
            }

            if (inlineValue is not null)
            {
                AddValue(values, name, inlineValue);
                continue;
            }

            var consumed = false;
            while (i + 1 < args.Length && !LooksLikeKnownOption(args[i + 1], knownOptions))
            {
                i++;
                AddValue(values, name, args[i]);
                consumed = true;
            }

            if (!consumed)
            {
                throw new ArgumentException($"Argument '{token}' requires at least one value.");
            }
        }

        return (values, switches);
    }

    /// <summary>
    /// Reads whether a parsed switch was present.
    /// Switch names are normalized through the same dash and underscore rules as parsing.
    /// Missing switches return <see langword="false"/> without mutating the parsed set.
    /// </summary>
    /// <param name="switches">The parsed switches.</param>
    /// <param name="name">The switch name.</param>
    /// <returns><see langword="true"/> when the switch was present.</returns>
    internal static bool GetSwitch(HashSet<string> switches, string name)
    {
        return switches.Contains(NormalizeOptionName(name));
    }

    /// <summary>
    /// Reads the last value for an option.
    /// Option names are normalized before lookup.
    /// A caller-provided default is returned when the option was not present.
    /// </summary>
    /// <param name="values">The parsed option values.</param>
    /// <param name="name">The option name.</param>
    /// <param name="defaultValue">The value to return when absent.</param>
    /// <returns>The option value or default value.</returns>
    internal static string GetString(Dictionary<string, List<string>> values, string name, string defaultValue = "")
    {
        return values.TryGetValue(NormalizeOptionName(name), out List<string>? optionValues) && optionValues.Count != 0
            ? optionValues[^1]
            : defaultValue;
    }

    /// <summary>
    /// Reads array values for an option.
    /// Values may be comma-split for normal options or preserved as raw trailing arguments.
    /// Missing options return the caller-provided default or an empty array.
    /// </summary>
    /// <param name="values">The parsed option values.</param>
    /// <param name="name">The option name.</param>
    /// <param name="defaultValue">The value to return when absent.</param>
    /// <param name="splitCommas">Whether comma-delimited values should split.</param>
    /// <returns>The option values.</returns>
    internal static string[] GetStringArray(Dictionary<string, List<string>> values, string name, string[]? defaultValue = null, bool splitCommas = true)
    {
        if (!values.TryGetValue(NormalizeOptionName(name), out List<string>? optionValues) || optionValues.Count == 0)
        {
            return defaultValue ?? [];
        }

        var result = new List<string>();
        foreach (string value in optionValues)
        {
            if (!splitCommas)
            {
                result.Add(value);
                continue;
            }

            foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                result.Add(part);
            }
        }

        return [.. result];
    }

    /// <summary>
    /// Finds the repository root for a file-based app.
    /// The search starts at the caller source file path so direct script execution is stable.
    /// The current working directory is used as a fallback for unusual launchers.
    /// </summary>
    /// <param name="sourceFilePath">The caller source file path.</param>
    /// <returns>The repository root path.</returns>
    internal static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        string? sourceDirectory = Path.GetDirectoryName(sourceFilePath);
        DirectoryInfo? directory = !string.IsNullOrWhiteSpace(sourceDirectory)
            ? new DirectoryInfo(sourceDirectory)
            : new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dotsider.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        directory = new DirectoryInfo(Directory.GetCurrentDirectory());
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

    /// <summary>
    /// Resolves an existing path relative to a base directory.
    /// </summary>
    /// <param name="pathValue">The input path.</param>
    /// <param name="description">The path description for diagnostics.</param>
    /// <param name="baseDirectory">The base directory for relative paths.</param>
    /// <returns>The full resolved path.</returns>
    internal static string ResolveExistingPath(string pathValue, string description, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            throw new ArgumentException($"{description} is required.");
        }

        string resolvedPathValue = Path.IsPathFullyQualified(pathValue)
            ? pathValue
            : Path.Combine(baseDirectory, pathValue);
        if (File.Exists(resolvedPathValue) || Directory.Exists(resolvedPathValue))
        {
            return Path.GetFullPath(resolvedPathValue);
        }

        throw new FileNotFoundException($"{description} '{pathValue}' does not exist.");
    }

    /// <summary>
    /// Resolves the requested process working directory.
    /// </summary>
    /// <param name="workingDirectory">The requested working directory.</param>
    /// <returns>The resolved working directory.</returns>
    internal static string ResolveWorkingDirectory(string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"Working directory '{workingDirectory}' does not exist.");
        }

        return Path.GetFullPath(workingDirectory);
    }

    /// <summary>
    /// Resolves an executable path from a path or PATH.
    /// </summary>
    /// <param name="commandPath">The command path or command name.</param>
    /// <param name="description">The command description for diagnostics.</param>
    /// <returns>The resolved executable path.</returns>
    internal static string ResolveCommandPath(string commandPath, string description)
    {
        if (File.Exists(commandPath))
        {
            return Path.GetFullPath(commandPath);
        }

        string? resolved = FindOnPath(commandPath);
        if (resolved is not null)
        {
            return resolved;
        }

        throw new FileNotFoundException($"Could not find {description} '{commandPath}'.");
    }

    /// <summary>
    /// Runs an external process and captures bounded output.
    /// The process streams are drained fully so large oracle output cannot deadlock or exhaust memory.
    /// Captured text is truncated after the requested character limit for each stream.
    /// </summary>
    /// <param name="filePath">The process executable.</param>
    /// <param name="arguments">The process arguments.</param>
    /// <param name="workingDirectory">The process working directory.</param>
    /// <param name="maxOutputCharacters">The maximum characters to retain per stream.</param>
    /// <param name="timeout">The optional process timeout.</param>
    /// <returns>The exit code, stdout, stderr, truncation state, and timeout state.</returns>
    internal static (int ExitCode, string Stdout, string Stderr, bool StdoutTruncated, bool StderrTruncated, bool TimedOut) RunProcess(
        string filePath,
        IEnumerable<string> arguments,
        string workingDirectory,
        int maxOutputCharacters = int.MaxValue,
        TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo(filePath)
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

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start '{filePath}'.");
        Task<(string Text, bool Truncated)> stdoutTask = Task.Run(() => ReadBoundedToEnd(process.StandardOutput, maxOutputCharacters));
        Task<(string Text, bool Truncated)> stderrTask = Task.Run(() => ReadBoundedToEnd(process.StandardError, maxOutputCharacters));
        var timedOut = false;
        if (timeout is { } processTimeout)
        {
            int milliseconds = checked((int)Math.Min(processTimeout.TotalMilliseconds, int.MaxValue));
            if (milliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
            }

            if (!process.WaitForExit(milliseconds))
            {
                timedOut = true;
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The process exited between WaitForExit and Kill.
                }

                process.WaitForExit();
            }
        }
        else
        {
            process.WaitForExit();
        }

        Task.WaitAll(stdoutTask, stderrTask);
        string stderr = stderrTask.Result.Text;
        if (timedOut)
        {
            stderr += $"{Environment.NewLine}[process killed after {timeout!.Value}]";
        }

        int exitCode = timedOut ? -1 : process.ExitCode;
        return (exitCode, stdoutTask.Result.Text, stderr, stdoutTask.Result.Truncated, stderrTask.Result.Truncated, timedOut);
    }

    /// <summary>
    /// Runs git in a repository and returns trimmed stdout.
    /// </summary>
    /// <param name="repositoryPath">The repository path.</param>
    /// <param name="arguments">The git arguments.</param>
    /// <returns>The trimmed stdout, or an empty string on failure.</returns>
    internal static string TryRunGit(string repositoryPath, params string[] arguments)
    {
        if (!Directory.Exists(repositoryPath))
        {
            return string.Empty;
        }

        var gitArguments = new List<string> { "-C", repositoryPath };
        gitArguments.AddRange(arguments);
        (int exitCode, string stdout, _, _, _, _) = RunProcess("git", gitArguments, Directory.GetCurrentDirectory());
        return exitCode == 0 ? stdout.Trim() : string.Empty;
    }

    /// <summary>
    /// Reads a text stream to completion while retaining only a bounded prefix.
    /// This keeps large external-tool captures reviewable without unbounded memory growth.
    /// The caller still gets a truncation flag for metadata and diagnostics.
    /// </summary>
    /// <param name="reader">The stream reader to drain.</param>
    /// <param name="maxCharacters">The maximum characters to retain.</param>
    /// <returns>The retained text and whether additional content was discarded.</returns>
    private static (string Text, bool Truncated) ReadBoundedToEnd(
        StreamReader reader,
        int maxCharacters)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxCharacters);

        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var buffer = new char[8192];
        var truncated = false;
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            int remaining = maxCharacters - builder.Length;
            if (remaining > 0)
            {
                int count = Math.Min(read, remaining);
                builder.Append(buffer, 0, count);
            }

            if (read > remaining)
            {
                truncated = true;
            }
        }

        if (truncated)
        {
            builder.AppendLine();
            builder.AppendLine($"[output truncated after {maxCharacters} characters]");
        }

        return (builder.ToString(), truncated);
    }

    /// <summary>
    /// Computes a file SHA-256 hash.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The lowercase hexadecimal hash.</returns>
    internal static string GetFileSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// Writes text using UTF-8 without BOM.
    /// </summary>
    /// <param name="path">The output file path.</param>
    /// <param name="content">The file content.</param>
    internal static void WriteTextFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, content, s_utf8NoBom);
    }

    /// <summary>
    /// Writes formatted JSON using UTF-8 without BOM.
    /// </summary>
    /// <param name="path">The output file path.</param>
    /// <param name="node">The JSON value to write.</param>
    internal static void WriteJsonFile(string path, JsonNode node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, node.ToJsonString(s_jsonOptions) + Environment.NewLine, s_utf8NoBom);
    }

    /// <summary>
    /// Converts strings to a JSON array.
    /// </summary>
    /// <param name="values">The values to convert.</param>
    /// <returns>The JSON array.</returns>
    internal static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (string value in values)
        {
            JsonNode? node = JsonValue.Create(value);
            array.Add(node);
        }

        return array;
    }

    /// <summary>
    /// Reads the next token as a required option value.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="index">The current token index.</param>
    /// <param name="token">The option token for diagnostics.</param>
    /// <returns>The required option value.</returns>
    private static string ReadRequiredValue(string[] args, ref int index, string token)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Argument '{token}' requires a value.");
        }

        string value = args[++index];
        if (value.Equals("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Argument '{token}' requires a value.");
        }

        return value;
    }

    /// <summary>
    /// Adds a parsed option value to the value map.
    /// </summary>
    /// <param name="values">The parsed value map.</param>
    /// <param name="name">The normalized option name.</param>
    /// <param name="value">The option value.</param>
    private static void AddValue(Dictionary<string, List<string>> values, string name, string value)
    {
        if (!values.TryGetValue(name, out List<string>? optionValues))
        {
            optionValues = [];
            values.Add(name, optionValues);
        }

        optionValues.Add(value);
    }

    /// <summary>
    /// Parses one option token and optional inline value.
    /// </summary>
    /// <param name="token">The command-line token.</param>
    /// <param name="knownOptions">The normalized known option names.</param>
    /// <param name="name">The parsed normalized option name.</param>
    /// <param name="inlineValue">The parsed inline value, when present.</param>
    /// <returns><see langword="true"/> when the token names a known option.</returns>
    private static bool TryParseOptionToken(
        string token,
        HashSet<string> knownOptions,
        out string name,
        out string? inlineValue)
    {
        name = string.Empty;
        inlineValue = null;
        if (!token.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        string body = token.TrimStart('-');
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        int equalsIndex = body.IndexOf('=', StringComparison.Ordinal);
        if (equalsIndex >= 0)
        {
            name = NormalizeOptionName(body[..equalsIndex]);
            inlineValue = body[(equalsIndex + 1)..];
            return knownOptions.Contains(name);
        }

        name = NormalizeOptionName(body);
        return knownOptions.Contains(name);
    }

    /// <summary>
    /// Detects whether a token starts a known option.
    /// </summary>
    /// <param name="token">The command-line token.</param>
    /// <param name="knownOptions">The normalized known option names.</param>
    /// <returns><see langword="true"/> when the token starts a known option.</returns>
    private static bool LooksLikeKnownOption(string token, HashSet<string> knownOptions)
    {
        return TryParseOptionToken(token, knownOptions, out _, out _);
    }

    /// <summary>
    /// Normalizes option names across dash and underscore spellings.
    /// </summary>
    /// <param name="name">The option name.</param>
    /// <returns>The normalized option name.</returns>
    private static string NormalizeOptionName(string name)
    {
        return name.TrimStart('-').Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses an inline switch value.
    /// </summary>
    /// <param name="value">The inline value.</param>
    /// <param name="token">The original option token for diagnostics.</param>
    /// <returns>The parsed boolean value.</returns>
    private static bool ParseBoolean(string value, string token)
    {
        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Argument '{token}' expects a boolean value.");
    }

    /// <summary>
    /// Finds a command on the current PATH.
    /// </summary>
    /// <param name="command">The command name.</param>
    /// <returns>The resolved command path, or <see langword="null"/>.</returns>
    private static string? FindOnPath(string command)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string[] extensions = OperatingSystem.IsWindows()
            ? Environment.GetEnvironmentVariable("PATHEXT")?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? [".exe", ".cmd", ".bat"]
            : [string.Empty];
        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string extension in extensions)
            {
                string candidate = Path.Combine(directory, command.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? command : command + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
