using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Dotsider.Benchmarks;

/// <summary>
/// Shared helpers for benchmark setup: repo discovery, sample building, and publish.
/// Uses static caching so each sample is built once per process even if multiple
/// benchmark classes reference the same fixture.
/// </summary>
internal static class BenchmarkHelpers
{
    private static readonly ConcurrentDictionary<string, string> BuildCache = new();
    private static readonly string s_buildOutputRoot =
        string.Equals(
            Environment.GetEnvironmentVariable("DOTSIDER_DEV_CONTAINER"),
            "1",
            StringComparison.Ordinal)
            ? Path.Combine("bin", "devcontainer")
            : "bin";
    private static string? _repoRoot;

    /// <summary>
    /// Discovers the repository root by walking up from <see cref="AppContext.BaseDirectory"/>
    /// looking for <c>Dotsider.slnx</c>.
    /// </summary>
    internal static string GetRepoRoot()
    {
        if (_repoRoot is not null)
            return _repoRoot;

        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "Dotsider.slnx")))
            {
                _repoRoot = dir;
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not find repo root (Dotsider.slnx) from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Builds a sample project with <c>dotnet build</c>. Cached per <paramref name="relativePath"/>.
    /// </summary>
    /// <param name="relativePath">Path relative to repo root (e.g. "samples/HelloWorld").</param>
    /// <returns>The absolute path to the project directory.</returns>
    internal static string BuildSample(string relativePath)
    {
        var projectDir = Path.Combine(GetRepoRoot(), relativePath);
        return BuildCache.GetOrAdd($"build:{relativePath}", _ =>
        {
            RunDotNet(projectDir, "build -c Debug -v q");
            return projectDir;
        });
    }

    /// <summary>
    /// Publishes a sample project as a self-contained single-file bundle.
    /// Cached per <paramref name="relativePath"/>.
    /// </summary>
    /// <param name="relativePath">Path relative to repo root (e.g. "samples/SelfContainedConsole").</param>
    /// <returns>The absolute path to the project directory.</returns>
    internal static string PublishSelfContainedSample(string relativePath)
    {
        var projectDir = Path.Combine(GetRepoRoot(), relativePath);
        return BuildCache.GetOrAdd($"publish:{relativePath}", _ =>
        {
            var rid = RuntimeInformation.RuntimeIdentifier;
            RunDotNet(projectDir,
                $"publish -c Release -r {rid} --self-contained -p:PublishSingleFile=true -v q");
            return projectDir;
        });
    }

    /// <summary>
    /// Publishes a sample project with Native AOT. Cached per <paramref name="relativePath"/>.
    /// </summary>
    /// <param name="relativePath">Path relative to repo root (e.g. "samples/NativeAotConsole").</param>
    /// <returns>The absolute path to the project directory.</returns>
    internal static string PublishNativeAotSample(string relativePath)
    {
        var projectDir = Path.Combine(GetRepoRoot(), relativePath);
        return BuildCache.GetOrAdd($"publish-aot:{relativePath}", _ =>
        {
            var rid = RuntimeInformation.RuntimeIdentifier;
            RunDotNet(projectDir, $"publish -c Release -r {rid} -v q");
            return projectDir;
        });
    }

    /// <summary>
    /// Returns the platform-specific apphost extension (e.g. ".exe" on Windows, "" on Unix).
    /// </summary>
    internal static string ApphostExtension =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

    /// <summary>
    /// Computes the publish output path for a self-contained sample.
    /// </summary>
    internal static string GetPublishPath(string relativePath, string assemblyName)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        return Path.Combine(GetRepoRoot(), relativePath, s_buildOutputRoot, "Release", "net10.0", rid, "publish",
            assemblyName + ApphostExtension);
    }

    /// <summary>
    /// Computes the Debug build output path for a sample.
    /// </summary>
    internal static string GetBuildPath(string relativePath, string fileName)
        => Path.Combine(GetRepoRoot(), relativePath, s_buildOutputRoot, "Debug", "net10.0", fileName);

    private static void RunDotNet(string workingDirectory, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // NoDefaultCurrentDirectoryInExePath breaks the ILCompiler's findvcvarsall →
        // VsDevCmd toolchain discovery during Native AOT publish. Clear it for this
        // process only (harmless for plain builds).
        psi.Environment.Remove("NoDefaultCurrentDirectoryInExePath");

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start: dotnet {arguments}");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"dotnet {arguments} failed in {workingDirectory} (exit {process.ExitCode}):\n{stdout}\n{stderr}");
    }
}
