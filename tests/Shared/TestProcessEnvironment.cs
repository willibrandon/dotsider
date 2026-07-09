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
    /// Removes inherited code-coverage profiler variables from <paramref name="startInfo"/>.
    /// </summary>
    /// <param name="startInfo">The child process start info to sanitize.</param>
    internal static void RemoveCodeCoverageVariables(ProcessStartInfo startInfo)
    {
        foreach (var variable in CodeCoverageEnvironmentVariables)
            startInfo.Environment.Remove(variable);
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
