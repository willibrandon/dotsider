namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the MCP prompts catalog and parameterized prompt retrieval.
/// </summary>
public class PromptTests : McpServerTestBase
{
    /// <summary>
    /// ListPrompts returns every prompt the server advertises by canonical name.
    /// </summary>
    [Fact]
    public async Task ListPrompts_ReturnsAllRegisteredPrompts()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var prompts = await client.ListPromptsAsync(cancellationToken: TestCancellationToken);

        Assert.True(prompts.Count >= 4);
        var names = prompts.Select(p => p.Name).ToList();
        Assert.Contains("security_audit", names);
        Assert.Contains("api_review", names);
        Assert.Contains("breaking_change_detection", names);
        Assert.Contains("dependency_health", names);
    }

    /// <summary>
    /// The prompt catalog includes the bundle_analysis prompt registered alongside bundle tools.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ListPrompts_IncludesBundleAnalysis()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        var prompts = await client.ListPromptsAsync(cancellationToken: TestCancellationToken);
        Assert.Contains(prompts, p => p.Name == "bundle_analysis");
    }

    /// <summary>
    /// security_audit materializes at least one message when given an assembly path.
    /// </summary>
    [Fact]
    public async Task GetPrompt_SecurityAudit_ReturnsPromptContent()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.GetPromptAsync(
            "security_audit",
            new Dictionary<string, object?> { ["assemblyPath"] = "/test/path.dll" },
            cancellationToken: TestCancellationToken);

        Assert.NotNull(result);
        Assert.True(result.Messages.Count > 0);
    }

    /// <summary>
    /// breaking_change_detection accepts both old and new assembly paths and produces messages.
    /// </summary>
    [Fact]
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

        Assert.NotNull(result);
        Assert.True(result.Messages.Count > 0);
    }
}
