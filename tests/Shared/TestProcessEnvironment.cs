using System.Collections;
using System.Diagnostics;

/// <summary>
/// Helpers for launching child processes from tests without inheriting the code-coverage profiler
/// environment injected into the test host.
/// </summary>
internal static class TestProcessEnvironment
{
    // Matches the environment variables stripped by MSTest's own child-process test helpers.
    private static readonly HashSet<string> CodeCoverageEnvironmentVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "MicrosoftInstrumentationEngine_ConfigPath32_VanguardInstrumentationProfiler",
        "MicrosoftInstrumentationEngine_ConfigPath64_VanguardInstrumentationProfiler",
        "CORECLR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_64",
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "COR_PROFILER_PATH_32",
        "COR_PROFILER_PATH_64",
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "CODE_COVERAGE_SESSION_NAME",
        "CODE_COVERAGE_PIPE_PATH",
        "MicrosoftInstrumentationEngine_LogLevel",
        "MicrosoftInstrumentationEngine_DisableCodeSignatureValidation",
        "MicrosoftInstrumentationEngine_FileLogPath",
    };

    /// <summary>
    /// Gets the isolated configuration used for fixture Debug builds in the development container.
    /// </summary>
    internal static string DebugBuildConfiguration => IsDevelopmentContainer ? "DevContainerDebug" : "Debug";

    /// <summary>
    /// Gets the isolated configuration used for fixture Release builds in the development container.
    /// </summary>
    internal static string ReleaseBuildConfiguration => IsDevelopmentContainer ? "DevContainerRelease" : "Release";

    /// <summary>
    /// Gets the configuration containing the currently executing test assembly.
    /// </summary>
    internal static string CurrentBuildConfiguration
        => GetBuildConfiguration(AppContext.BaseDirectory);

    /// <summary>
    /// Extracts the configuration from a conventional or development-container output path.
    /// </summary>
    /// <param name="baseDirectory">The application base directory.</param>
    /// <returns>The detected configuration, or Debug when no output segment is present.</returns>
    internal static string GetBuildConfiguration(string baseDirectory)
    {
        string[] parts = baseDirectory.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (!parts[index].Equals("bin", StringComparison.OrdinalIgnoreCase))
                continue;

            return parts[index + 1].Equals("devcontainer", StringComparison.OrdinalIgnoreCase)
                && index + 2 < parts.Length
                ? parts[index + 2]
                : parts[index + 1];
        }

        return "Debug";
    }

    /// <summary>
    /// Gets whether the current test process is running inside the dotsider development container.
    /// </summary>
    internal static bool IsDevelopmentContainer =>
        string.Equals(Environment.GetEnvironmentVariable("DOTSIDER_DEV_CONTAINER"), "1", StringComparison.Ordinal);

    /// <summary>
    /// Resolves a project's build output directory for the active host or development container.
    /// </summary>
    /// <param name="projectDirectory">The project directory.</param>
    /// <param name="configuration">The build configuration.</param>
    /// <param name="targetFramework">The target framework directory.</param>
    /// <returns>The absolute project output directory.</returns>
    internal static string GetProjectOutputDirectory(
        string projectDirectory,
        string configuration,
        string targetFramework) =>
        IsDevelopmentContainer
            ? Path.Combine(projectDirectory, "bin", "devcontainer", configuration, targetFramework)
            : Path.Combine(projectDirectory, "bin", configuration, targetFramework);

    /// <summary>
    /// Removes inherited code-coverage profiler variables from <paramref name="startInfo"/>.
    /// </summary>
    /// <param name="startInfo">The child process start info to sanitize.</param>
    internal static void RemoveCodeCoverageVariables(ProcessStartInfo startInfo)
    {
        foreach (var variable in CodeCoverageEnvironmentVariables)
            startInfo.Environment.Remove(variable);
    }

    /// <summary>
    /// Removes profiler variables and gives fixture builds an isolated, conventional intermediate layout.
    /// </summary>
    /// <param name="startInfo">The child build process start info to configure.</param>
    internal static void ConfigureBuild(ProcessStartInfo startInfo)
    {
        RemoveCodeCoverageVariables(startInfo);
        if (!IsDevelopmentContainer)
            return;

        startInfo.Environment["BaseIntermediateOutputPath"] = "obj/";
        startInfo.Environment["MSBuildProjectExtensionsPath"] = "obj/devcontainer/";
        startInfo.Environment["DOTSIDER_FIXTURE_BUILD"] = "1";
        startInfo.Environment.Remove("DefaultItemExcludes");
    }

    /// <summary>
    /// Removes repository build overrides so file-based apps retain their SDK-managed cache layout.
    /// </summary>
    /// <param name="startInfo">The file-based app process start info to configure.</param>
    internal static void ConfigureFileApp(ProcessStartInfo startInfo)
    {
        RemoveCodeCoverageVariables(startInfo);
        if (!IsDevelopmentContainer)
            return;

        startInfo.Environment.Remove("BaseIntermediateOutputPath");
        startInfo.Environment.Remove("DefaultItemExcludes");
        startInfo.Environment.Remove("MSBuildProjectExtensionsPath");
    }

    /// <summary>
    /// Removes repository overrides so UseArtifactsOutput can own the fixture's isolated layout.
    /// </summary>
    /// <param name="startInfo">The artifacts-layout build process start info to configure.</param>
    internal static void ConfigureArtifactsBuild(ProcessStartInfo startInfo)
    {
        ConfigureFileApp(startInfo);
        if (IsDevelopmentContainer)
        {
            startInfo.Environment["ArtifactsPath"] = "artifacts/devcontainer/";
            startInfo.Environment["DefaultItemExcludes"] = "obj/**;bin/**;artifacts/**";
            startInfo.Environment["DOTSIDER_FIXTURE_BUILD"] = "1";
        }
    }

    /// <summary>
    /// Builds a copy of the current process environment without code-coverage profiler variables.
    /// </summary>
    /// <returns>A clean environment dictionary suitable for APIs that do not expose <see cref="ProcessStartInfo"/>.</returns>
    internal static Dictionary<string, string?> WithoutCodeCoverage()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = (string)entry.Key;
            if (CodeCoverageEnvironmentVariables.Contains(key))
                continue;

            environment[key] = entry.Value?.ToString();
        }

        return environment;
    }

    /// <summary>
    /// Builds a string-valued copy of the current process environment without code-coverage profiler variables.
    /// </summary>
    /// <returns>A clean environment dictionary suitable for terminal process helpers.</returns>
    internal static Dictionary<string, string> WithoutCodeCoverageStringValues()
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = (string)entry.Key;
            if (CodeCoverageEnvironmentVariables.Contains(key))
                continue;

            if (entry.Value is string value)
                environment[key] = value;
        }

        return environment;
    }
}
