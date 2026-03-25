using Dotsider.Core.Protocol;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests that <see cref="RemoteDotsiderTarget"/> correctly rejects old or
/// mismatched server responses.
/// </summary>
public class RemoteDotsiderTargetVersionTests
{
    [Fact(Timeout = 10_000)]
    public async Task RejectsOldServerResponse()
    {
        var socketPath = GetUniqueSocketPath();
        await using var testSocket = new TestRawJsonSocket(socketPath);
        testSocket.OnRequest(_ => """{"success":true}""");
        testSocket.Start();

        var target = new RemoteDotsiderTarget(socketPath);
        var response = await target.SendAsync(
            new DotsiderRequest { Method = "assembly-info" },
            TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Contains("server response", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Timeout = 10_000)]
    public async Task RejectsWrongServerVersion()
    {
        var socketPath = GetUniqueSocketPath();
        await using var testSocket = new TestRawJsonSocket(socketPath);
        testSocket.OnRequest(_ => """{"v":99,"success":true}""");
        testSocket.Start();

        var target = new RemoteDotsiderTarget(socketPath);
        var response = await target.SendAsync(
            new DotsiderRequest { Method = "assembly-info" },
            TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Contains("version mismatch", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetUniqueSocketPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotsider", "sockets");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"test-{Random.Shared.Next(100_000, 999_999)}.dotsider.socket");
    }
}
