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
        Assert.Contains("IntermediateOutputPath", project);
        Assert.Contains("OutputPath", project);
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
    /// Verifies each bundled trace-host publish uses consumer-specific build directories.
    /// Parallel solution builds must not share TraceHost intermediate or output files.
    /// This prevents concurrent GenerateDepsFile tasks from writing the same dependency file.
    /// </summary>
    [TestMethod]
    public void TraceHostPublish_UsesConsumerSpecificBuildDirectories()
    {
        string targets = File.ReadAllText(Path.Combine(
            TestHelpers.GetRepoRoot(),
            "build",
            "Dotsider.TraceHost.targets"));

        Assert.Contains(
            "$(BaseIntermediateOutputPath)tracehost\\$(MSBuildProjectName)\\$(Configuration)\\",
            targets);
        Assert.Contains(
            "IntermediateOutputPath=$(_DotsiderTraceHostIntermediateDirectory)",
            targets);
        Assert.Contains("OutputPath=$(_DotsiderTraceHostOutputDirectory)", targets);
        Assert.Contains(
            "<_DotsiderTraceHostPublishDirectory>$(_DotsiderTraceHostBuildDirectory)publish\\",
            targets);
        Assert.Contains(
            "<Target Name=\"CleanDotsiderTraceHostBuild\"",
            targets);
        Assert.Contains(
            "<RemoveDir Directories=\"$(_DotsiderTraceHostBuildDirectory)\" />",
            targets);

        string testProject = File.ReadAllText(Path.Combine(
            TestHelpers.GetRepoRoot(),
            "tests",
            "Dotsider.Tests",
            "Dotsider.Tests.csproj"));

        Assert.Contains(
            "<IncludeDotsiderTraceHostInOutput>true</IncludeDotsiderTraceHostInOutput>",
            testProject);
        Assert.Contains(
            "<Import Project=\"../../build/Dotsider.TraceHost.targets\" />",
            testProject);
        Assert.DoesNotContain("CopyDotsiderTraceHostToTestOutput", testProject);
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

    /// <summary>
    /// Verifies CI integrations are released only after their generated runtime, real
    /// cross-platform execution, checksums, attestations, and marketplace packages are checked.
    /// </summary>
    [TestMethod]
    public void CiIntegrations_KeepReleaseVerificationAndPublicationGates()
    {
        string root = TestHelpers.GetRepoRoot();
        string release = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));
        string ci = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        string action = File.ReadAllText(Path.Combine(root, "action.yml"));
        string task = File.ReadAllText(Path.Combine(
            root, "azure-devops", "tasks", "DotsiderSizeCheckV1", "task.json"));

        Assert.Contains("build-ci-integrations:", release);
        Assert.Contains("verify-size-check-release:", release);
        Assert.Contains("publish-azure-devops-marketplace:", release);
        Assert.Contains("publish-github-action:", release);
        Assert.Contains("verify-github-marketplace:", release);
        Assert.Contains("actions/attest@v4.2.2", release);
        Assert.Contains("if: \"!contains(github.ref_name, '-')\"", release);
        Assert.Contains("gh attestation verify", release);
        Assert.Contains("AZURE_DEVOPS_MARKETPLACE_PAT", release);
        Assert.Contains("pnpm exec tfx extension publish", release);
        Assert.Contains("dotsider-azure-devops-extension/*.vsix", release);
        Assert.Contains("Expected one Azure DevOps VSIX and checksum", release);
        Assert.DoesNotContain("for file in azure-devops-extension/*.vsix", release);
        Assert.Contains("$task.version.Major = 1", release);
        Assert.Contains("$task.version.Patch = ([int]$versionParts[1] * 1000)", release);
        Assert.Contains("needs: [verify-size-check-release, build-ci-integrations]", release);
        Assert.Contains("if: \"!contains(github.ref_name, '-')\"", release);
        Assert.Contains("Size check integration (${{ matrix.rid }})", ci);
        Assert.Contains("windows-11-arm", ci);
        Assert.Contains("macos-26-intel", ci);
        Assert.Contains("pnpm --dir integrations/size-check test:integration", ci);
        Assert.Contains("uses: ./", ci);
        int integrationJobStart = ci.IndexOf("  size-check-integrations:", StringComparison.Ordinal);
        int integrationJobEnd = ci.IndexOf("  deploy-tests:", integrationJobStart, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, integrationJobStart);
        Assert.IsGreaterThan(integrationJobStart, integrationJobEnd);
        string integrationJob = ci[integrationJobStart..integrationJobEnd];
        Assert.DoesNotContain("continue-on-error", integrationJob);
        Assert.DoesNotContain("Run action with a failing budget", integrationJob);
        Assert.DoesNotContain("Run action with invalid input", integrationJob);
        Assert.Contains("using: composite", action);
        Assert.Contains("node-version: '24'", action);
        Assert.Contains("Node24", task);
        Assert.Contains("Node20_1", task);
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
