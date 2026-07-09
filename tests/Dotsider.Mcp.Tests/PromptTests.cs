namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the MCP prompts catalog and parameterized prompt retrieval.
/// </summary>
[TestClass]
public class PromptTests : McpServerTestBase
{
    /// <summary>
    /// ListPrompts returns every prompt the server advertises by canonical name.
    /// </summary>
    [TestMethod]
    public async Task ListPrompts_ReturnsAllRegisteredPrompts()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var prompts = await client.ListPromptsAsync(cancellationToken: TestCancellationToken);

        Assert.IsGreaterThanOrEqualTo(4, prompts.Count);
        var names = prompts.Select(p => p.Name).ToList();
        Assert.Contains("security_audit", names);
        Assert.Contains("api_review", names);
        Assert.Contains("breaking_change_detection", names);
        Assert.Contains("dependency_health", names);
    }

    /// <summary>
    /// The prompt catalog includes the bundle_analysis prompt registered alongside bundle tools.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListPrompts_IncludesBundleAnalysis()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        var prompts = await client.ListPromptsAsync(cancellationToken: TestCancellationToken);
        Assert.Contains(p => p.Name == "bundle_analysis", prompts);
    }

    /// <summary>
    /// security_audit materializes at least one message when given an assembly path.
    /// </summary>
    [TestMethod]
    public async Task GetPrompt_SecurityAudit_ReturnsPromptContent()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.GetPromptAsync(
            "security_audit",
            new Dictionary<string, object?> { ["assemblyPath"] = "/test/path.dll" },
            cancellationToken: TestCancellationToken);

        Assert.IsNotNull(result);
        Assert.IsGreaterThan(0, result.Messages.Count);
    }

    /// <summary>
    /// breaking_change_detection accepts both old and new assembly paths and produces messages.
    /// </summary>
    [TestMethod]
    public async Task GetPrompt_BreakingChangeDetection_AcceptsTwoPaths()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.GetPromptAsync(
            "breaking_change_detection",
            new Dictionary<string, object?>
            {
                ["oldAssemblyPath"] = "/test/v1.dll",
                ["newAssemblyPath"] = "/test/v2.dll"
            },
            cancellationToken: TestCancellationToken);

        Assert.IsNotNull(result);
        Assert.IsGreaterThan(0, result.Messages.Count);
    }

    /// <summary>
    /// dependency_health's materialized prompt must describe the tool's actual capabilities —
    /// transitive closure traversal including diamond dependencies, circular references, and
    /// transitive depth — and must acknowledge that framework assemblies are included so
    /// models don't expect the tool to filter them. Asserting content (not just registration)
    /// catches future prompt/tool contract drift that was the original issue behind #149.
    /// </summary>
    [TestMethod]
    public async Task GetPrompt_DependencyHealth_ContentMentionsTransitiveClosureAndFrameworkInclusion()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.GetPromptAsync(
            "dependency_health",
            new Dictionary<string, object?> { ["assemblyPath"] = "/test/path.dll" },
            cancellationToken: TestCancellationToken);

        Assert.IsNotNull(result);
        var content = string.Join("\n", result.Messages.Select(m => m.Content.ToString()));
        Assert.Contains("transitive", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("diamond", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("circular", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("framework", content, StringComparison.OrdinalIgnoreCase);
    }
}
