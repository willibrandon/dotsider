using Dotsider.Core.Protocol;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests that <see cref="RemoteDotsiderTarget"/> correctly rejects old or
/// mismatched server responses.
/// </summary>
[TestClass]
public class RemoteDotsiderTargetVersionTests
{
    /// <summary>
    /// A pre-versioned server response is rejected because the protocol contract requires a 'v' field.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task RejectsOldServerResponse()
    {
        var socketPath = GetUniqueSocketPath();
        await using var testSocket = new TestRawJsonSocket(socketPath);
        testSocket.OnRequest(_ => """{"success":true}""");
        testSocket.Start();

        var target = new RemoteDotsiderTarget(socketPath);
        var response = await target.SendAsync(
            new DotsiderRequest { Method = "assembly-info" },
            CancellationToken.None);

        Assert.IsFalse(response.Success);
        Assert.Contains("server response", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rejects a remote server whose advertised version differs from the client target.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task RejectsWrongServerVersion()
    {
        var socketPath = GetUniqueSocketPath();
        await using var testSocket = new TestRawJsonSocket(socketPath);
        testSocket.OnRequest(_ => """{"v":99,"success":true}""");
        testSocket.Start();

        var target = new RemoteDotsiderTarget(socketPath);
        var response = await target.SendAsync(
            new DotsiderRequest { Method = "assembly-info" },
            CancellationToken.None);

        Assert.IsFalse(response.Success);
        Assert.Contains("version mismatch", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetUniqueSocketPath()
    {
        return Path.Combine(Path.GetTempPath(), $"mp-{Guid.NewGuid():N}");
    }
}
