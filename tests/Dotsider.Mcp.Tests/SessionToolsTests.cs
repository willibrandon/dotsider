using System.Text.Json;
using Dotsider.Core.Protocol;

namespace Dotsider.Mcp.Tests;

public class SessionToolsTests : McpServerTestBase
{
    // Use a PID that won't collide with real processes
    private const int TestPid = 999_999;

    [Fact]
    public async Task DiscoverDotsiderSessions_FindsRunningInstance()
    {
        await using var socket = new TestDotsiderSocket(TestPid, "/tmp/test/HelloWorld.dll");
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "discover_dotsider_sessions",
            new Dictionary<string, object?>(),
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        // Should find our test instance in the JSON array
        var sessions = JsonSerializer.Deserialize<JsonElement[]>(text);
        Assert.NotNull(sessions);

        var testSession = sessions!.FirstOrDefault(s =>
            s.GetProperty("pid").GetInt32() == TestPid);
        Assert.NotEqual(default, testSession);
        Assert.Equal(TestPid, testSession.GetProperty("pid").GetInt32());
    }

    [Fact]
    public async Task GetSessionInfo_ReturnsAssemblyAndViewData()
    {
        await using var socket = new TestDotsiderSocket(TestPid, "/tmp/test/HelloWorld.dll");

        // Add a get-current-view handler
        socket.OnMethod("get-current-view", _ => DotsiderResponse.Ok(new
        {
            Tab = 0,
            AssemblyPath = "/tmp/test/HelloWorld.dll"
        }));

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_session_info",
            new Dictionary<string, object?> { ["sessionId"] = TestPid },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);

        var doc = JsonDocument.Parse(text!);
        Assert.True(doc.RootElement.TryGetProperty("assembly", out _));
        Assert.True(doc.RootElement.TryGetProperty("view", out _));
    }
}
