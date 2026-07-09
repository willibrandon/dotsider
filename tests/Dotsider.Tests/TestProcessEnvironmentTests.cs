using System.Diagnostics;

namespace Dotsider.Tests;

/// <summary>
/// Tests for child-process environment sanitization used by integration tests.
/// </summary>
[TestClass]
public class TestProcessEnvironmentTests
{
    /// <summary>
    /// Code-coverage profiler variables are removed without disturbing unrelated environment variables.
    /// </summary>
    [TestMethod]
    public void RemoveCodeCoverageVariables_StripsProfilerEnvironment()
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
        };
        startInfo.Environment["CORECLR_ENABLE_PROFILING"] = "1";
        startInfo.Environment["CORECLR_PROFILER"] = "{00000000-0000-0000-0000-000000000000}";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        TestProcessEnvironment.RemoveCodeCoverageVariables(startInfo);

        Assert.IsFalse(startInfo.Environment.ContainsKey("CORECLR_ENABLE_PROFILING"));
        Assert.IsFalse(startInfo.Environment.ContainsKey("CORECLR_PROFILER"));
        Assert.AreEqual("1", startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"]);
    }
}
