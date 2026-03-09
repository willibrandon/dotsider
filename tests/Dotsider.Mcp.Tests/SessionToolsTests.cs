namespace Dotsider.Mcp.Tests;

public class SessionToolsTests : McpServerTestBase
{
    [Fact]
    public async Task DiscoverDotsiderSessions_NoRunningInstances_ReturnsMessage()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "discover_dotsider_sessions",
            new Dictionary<string, object?>(),
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("No running dotsider instances found", text);
    }
}
