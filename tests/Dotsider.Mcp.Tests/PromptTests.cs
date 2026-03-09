namespace Dotsider.Mcp.Tests;

public class PromptTests : McpServerTestBase
{
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
