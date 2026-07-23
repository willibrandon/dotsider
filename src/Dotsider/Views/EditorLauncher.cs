using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Dotsider.Views;

/// <summary>
/// Resolves configured editors and starts them without exposing source paths to shell parsing.
/// </summary>
internal static class EditorLauncher
{
    private const string BatchArgumentPrefix = "DOTSIDER_EDITOR_ARGUMENT_";
    private const string BatchScriptVariable = "DOTSIDER_EDITOR_SCRIPT";
    private const string BatchSourceVariable = "DOTSIDER_EDITOR_SOURCE";

    /// <summary>
    /// Launches an editor using the process environment and production process starter.
    /// </summary>
    /// <param name="store">The store that owns the source path.</param>
    /// <param name="sourcePath">The source path to open.</param>
    /// <param name="openedPath">The exact path selected for the launch attempt.</param>
    /// <returns>The editor launch status.</returns>
    internal static EditorLaunchStatus Launch(
        EmbeddedSourceTempFileStore store,
        string sourcePath,
        out string openedPath)
    {
        var pathEntries = EditorExecutableResolver.SplitPathEntries(
            Environment.GetEnvironmentVariable("PATH"));
        var pathExtensions = OperatingSystem.IsWindows()
            ? EditorExecutableResolver.SplitPathExtensions(
                Environment.GetEnvironmentVariable("PATHEXT"))
            : [];

        return Launch(
            store,
            sourcePath,
            Environment.GetEnvironmentVariable("VISUAL"),
            Environment.GetEnvironmentVariable("EDITOR"),
            pathEntries,
            pathExtensions,
            StartProcess,
            out openedPath);
    }

    /// <summary>
    /// Launches an editor using explicit configuration and a caller-provided process starter.
    /// </summary>
    /// <param name="store">The store that owns the source path.</param>
    /// <param name="sourcePath">The source path to open.</param>
    /// <param name="visual">The configured VISUAL value.</param>
    /// <param name="editor">The configured EDITOR value.</param>
    /// <param name="pathEntries">The PATH entries available for resolution.</param>
    /// <param name="pathExtensions">The PATHEXT entries available on Windows.</param>
    /// <param name="startProcess">The process-start operation.</param>
    /// <param name="openedPath">The exact path selected for the launch attempt.</param>
    /// <returns>The editor launch status.</returns>
    internal static EditorLaunchStatus Launch(
        EmbeddedSourceTempFileStore store,
        string sourcePath,
        string? visual,
        string? editor,
        IReadOnlyList<string> pathEntries,
        IReadOnlyList<string> pathExtensions,
        Func<ProcessStartInfo, IDisposable?> startProcess,
        out string openedPath)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(pathEntries);
        ArgumentNullException.ThrowIfNull(pathExtensions);
        ArgumentNullException.ThrowIfNull(startProcess);

        openedPath = sourcePath;
        var status = TryConfiguredEditor(
            visual,
            sourcePath,
            pathEntries,
            pathExtensions,
            startProcess);
        if (status is EditorLaunchStatus.Started or EditorLaunchStatus.Failed)
            return status;

        status = TryConfiguredEditor(
            editor,
            sourcePath,
            pathEntries,
            pathExtensions,
            startProcess);
        if (status is EditorLaunchStatus.Started or EditorLaunchStatus.Failed)
            return status;

        try
        {
            openedPath = store.PrepareAssociationPath(sourcePath);
            return TryStart(CreateAssociationStartInfo(openedPath), startProcess);
        }
        catch (Exception ex) when (IsExpectedLaunchException(ex))
        {
            return EditorLaunchStatus.Failed;
        }
    }

    /// <summary>
    /// Creates start information for a directly executable configured editor.
    /// </summary>
    /// <param name="executable">The resolved absolute editor path.</param>
    /// <param name="arguments">The configured literal editor arguments.</param>
    /// <param name="sourcePath">The absolute source path.</param>
    /// <returns>Shell-free process start information.</returns>
    internal static ProcessStartInfo CreateDirectStartInfo(
        string executable,
        IReadOnlyList<string> arguments,
        string sourcePath)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? ""
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.ArgumentList.Add(sourcePath);
        return startInfo;
    }

    /// <summary>
    /// Creates start information for a resolved Windows batch editor shim.
    /// </summary>
    /// <param name="resolvedScript">The resolved absolute batch script path.</param>
    /// <param name="arguments">The configured literal editor arguments.</param>
    /// <param name="sourcePath">The absolute source path.</param>
    /// <returns>Start information using the fully qualified system command interpreter.</returns>
    internal static ProcessStartInfo CreateWindowsBatchStartInfo(
        string resolvedScript,
        IReadOnlyList<string> arguments,
        string sourcePath)
    {
        if (arguments.Any(argument => argument.Contains('"')))
        {
            throw new ArgumentException(
                "Windows batch editor arguments cannot contain literal double quotes.",
                nameof(arguments));
        }

        var startInfo = new ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(resolvedScript) ?? ""
        };

        startInfo.Environment[BatchScriptVariable] = $".{resolvedScript}";
        startInfo.Environment[BatchSourceVariable] = $".{sourcePath}";

        for (var index = 0; index < arguments.Count; index++)
        {
            var variable = $"{BatchArgumentPrefix}{index:D4}";
            startInfo.Environment[variable] = $".{arguments[index]}";
        }

        startInfo.Arguments = BuildBatchArguments(arguments.Count);
        return startInfo;
    }

    /// <summary>
    /// Creates platform-association start information for an inert text source path.
    /// </summary>
    /// <param name="sourcePath">The absolute <c>.txt</c> source path.</param>
    /// <returns>Association-based process start information.</returns>
    internal static ProcessStartInfo CreateAssociationStartInfo(string sourcePath)
    {
        if (!string.Equals(Path.GetExtension(sourcePath), ".txt", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Association fallback requires a .txt source path.", nameof(sourcePath));

        return new ProcessStartInfo(sourcePath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(sourcePath) ?? ""
        };
    }

    private static bool IsExpectedLaunchException(Exception exception) =>
        exception is Win32Exception
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException;

    private static string BuildBatchArguments(int argumentCount)
    {
        var command = new StringBuilder($"\"\"%{BatchScriptVariable}:~1%\"");
        for (var index = 0; index < argumentCount; index++)
        {
            command.Append(" \"%");
            command.Append($"{BatchArgumentPrefix}{index:D4}");
            command.Append(":~1%\"");
        }

        command.Append(" \"%");
        command.Append(BatchSourceVariable);
        command.Append(":~1%\"\"");
        return $"/d /s /v:off /c {command}";
    }

    private static bool IsWindowsBatchFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase);
    }

    private static IDisposable? StartProcess(ProcessStartInfo startInfo) =>
        Process.Start(startInfo);

    private static EditorLaunchStatus TryConfiguredEditor(
        string? configuredValue,
        string sourcePath,
        IReadOnlyList<string> pathEntries,
        IReadOnlyList<string> pathExtensions,
        Func<ProcessStartInfo, IDisposable?> startProcess)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
            return EditorLaunchStatus.NotFound;

        if (!EditorCommand.TryParse(configuredValue, out var command))
            return EditorLaunchStatus.Failed;

        var resolved = OperatingSystem.IsWindows()
            ? EditorExecutableResolver.TryResolveWindows(
                command.Executable,
                pathEntries,
                pathExtensions,
                out string resolvedPath)
            : EditorExecutableResolver.TryResolveUnix(
                command.Executable,
                pathEntries,
                out resolvedPath);
        if (!resolved)
            return EditorLaunchStatus.NotFound;

        var startInfo = OperatingSystem.IsWindows() && IsWindowsBatchFile(resolvedPath)
            ? command.Arguments.Any(argument => argument.Contains('"'))
                ? null
                : CreateWindowsBatchStartInfo(resolvedPath, command.Arguments, sourcePath)
            : CreateDirectStartInfo(resolvedPath, command.Arguments, sourcePath);
        if (startInfo is null)
            return EditorLaunchStatus.Failed;

        return TryStart(startInfo, startProcess);
    }

    private static EditorLaunchStatus TryStart(
        ProcessStartInfo startInfo,
        Func<ProcessStartInfo, IDisposable?> startProcess)
    {
        try
        {
            using var process = startProcess(startInfo);
            return process is null
                ? EditorLaunchStatus.Failed
                : EditorLaunchStatus.Started;
        }
        catch (Exception ex) when (IsExpectedLaunchException(ex))
        {
            return EditorLaunchStatus.Failed;
        }
    }
}
