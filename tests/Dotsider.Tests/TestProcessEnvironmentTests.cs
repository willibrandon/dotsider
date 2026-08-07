using System.Diagnostics;

namespace Dotsider.Tests;

/// <summary>
/// Tests for child-process environment sanitization used by integration tests.
/// </summary>
[TestClass]
public class TestProcessEnvironmentTests
{
    /// <summary>
    /// Conventional and development-container output paths expose the actual configuration.
    /// </summary>
    /// <param name="useDevelopmentContainerLayout">Whether to include the isolated path segment.</param>
    /// <param name="configuration">The expected and embedded configuration.</param>
    [TestMethod]
    [DataRow(false, "Debug")]
    [DataRow(false, "Release")]
    [DataRow(true, "Debug")]
    [DataRow(true, "Release")]
    public void GetBuildConfiguration_KnownOutputLayout_ReturnsConfiguration(
        bool useDevelopmentContainerLayout,
        string configuration)
    {
        string baseDirectory = useDevelopmentContainerLayout
            ? Path.Combine("root", "bin", "devcontainer", configuration, "net10.0")
            : Path.Combine("root", "bin", configuration, "net10.0");

        string actual = TestProcessEnvironment.GetBuildConfiguration(baseDirectory);

        Assert.AreEqual(configuration, actual);
    }

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

    /// <summary>
    /// Artifact-layout fixture builds use a container-only artifacts root.
    /// </summary>
    [TestMethod]
    public void ConfigureArtifactsBuild_UsesContainerArtifactsRootOnlyInContainer()
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
        };

        TestProcessEnvironment.ConfigureArtifactsBuild(startInfo);

        if (TestProcessEnvironment.IsDevelopmentContainer)
        {
            Assert.AreEqual("artifacts/devcontainer/", startInfo.Environment["ArtifactsPath"]);
            Assert.AreEqual(
                "obj/**;bin/**;artifacts/**",
                startInfo.Environment["DefaultItemExcludes"]);
            Assert.AreEqual("1", startInfo.Environment["DOTSIDER_FIXTURE_BUILD"]);
        }
        else
        {
            Assert.IsFalse(startInfo.Environment.ContainsKey("ArtifactsPath"));
            Assert.IsFalse(startInfo.Environment.ContainsKey("DefaultItemExcludes"));
            Assert.IsFalse(startInfo.Environment.ContainsKey("DOTSIDER_FIXTURE_BUILD"));
        }
    }
}
