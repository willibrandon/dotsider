namespace Dotsider.Tests;

/// <summary>
/// Regression tests for release workflow behavior that is easy to break in YAML.
/// </summary>
[TestClass]
public class ReleaseWorkflowTests
{
    /// <summary>
    /// Verifies trace-host publish settings do not flow into the Core project reference.
    /// Multiple bundled trace-host publishes must share one Core project build.
    /// This prevents concurrent builds from writing the same Core output file.
    /// </summary>
    [TestMethod]
    public void TraceHostProjectReference_RemovesPublishGlobalProperties()
    {
        string project = File.ReadAllText(Path.Combine(
            TestHelpers.GetRepoRoot(),
            "src",
            "Dotsider.TraceHost",
            "Dotsider.TraceHost.csproj"));

        Assert.Contains("GlobalPropertiesToRemove=", project);
        Assert.Contains("PublishAot", project);
        Assert.Contains("PublishDir", project);
        Assert.Contains("PublishReadyToRun", project);
        Assert.Contains("PublishSingleFile", project);
        Assert.Contains("PublishTrimmed", project);
        Assert.Contains("RuntimeIdentifier", project);
        Assert.Contains("RuntimeIdentifiers", project);
        Assert.Contains("SelfContained", project);
        Assert.Contains("UseAppHost", project);
    }

    /// <summary>
    /// Verifies generated winget branch commits suppress fork-side push workflows without
    /// leaking the marker into user-facing winget pull request titles.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void WingetSubmissionCommits_SkipForkPushWorkflowsOnly()
    {
        var releaseWorkflow = File.ReadAllText(Path.Combine(
            TestHelpers.GetRepoRoot(),
            ".github",
            "workflows",
            "release.yml"));

        Assert.Contains("message=\"Update willibrandon.dotsider to $version [skip actions]\"", releaseWorkflow);
        Assert.Contains("message=\"Update willibrandon.dotsider-mcp to $version [skip actions]\"", releaseWorkflow);
        Assert.AreEqual(2, CountOccurrences(releaseWorkflow, "[skip actions]"));
        Assert.DoesNotContain("[skip ci]", releaseWorkflow);

        Assert.Contains("$prTitle = \"Update willibrandon.dotsider to $version\"", releaseWorkflow);
        Assert.Contains("$prTitle = \"New package: willibrandon.dotsider $version\"", releaseWorkflow);
        Assert.Contains("$prTitle = \"Update willibrandon.dotsider-mcp to $version\"", releaseWorkflow);
        Assert.Contains("$prTitle = \"New package: willibrandon.dotsider-mcp $version\"", releaseWorkflow);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var start = 0;
        while (true)
        {
            var index = text.IndexOf(value, start, StringComparison.Ordinal);
            if (index < 0)
                return count;

            count++;
            start = index + value.Length;
        }
    }
}
