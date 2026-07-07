namespace Dotsider.Tests;

/// <summary>
/// Regression tests for release workflow behavior that is easy to break in YAML.
/// </summary>
public class ReleaseWorkflowTests
{
    /// <summary>
    /// Verifies generated winget branch commits suppress fork-side push workflows without
    /// leaking the marker into user-facing winget pull request titles.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void WingetSubmissionCommits_SkipForkPushWorkflowsOnly()
    {
        var releaseWorkflow = File.ReadAllText(Path.Combine(
            TestHelpers.GetRepoRoot(),
            ".github",
            "workflows",
            "release.yml"));

        Assert.Contains("message=\"Update willibrandon.dotsider to $version [skip actions]\"", releaseWorkflow);
        Assert.Contains("message=\"Update willibrandon.dotsider-mcp to $version [skip actions]\"", releaseWorkflow);
        Assert.Equal(2, CountOccurrences(releaseWorkflow, "[skip actions]"));
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
