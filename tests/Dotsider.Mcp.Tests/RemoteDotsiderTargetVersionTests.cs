using Dotsider.Core.Protocol;
using System.Text;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests that <see cref="RemoteDotsiderTarget"/> correctly rejects old or
/// mismatched server responses.
/// </summary>
/// <param name="testContext">The context for the current test.</param>
[TestClass]
public sealed class RemoteDotsiderTargetVersionTests(TestContext testContext)
{
    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Oversized typed requests fail before attempting a socket connection.
    /// </summary>
    [TestMethod]
    public async Task SendAsync_OversizedRequest_FailsBeforeConnect()
    {
        var target = new RemoteDotsiderTarget(GetUniqueSocketPath());

        var response = await target.SendAsync(
            new DotsiderRequest
            {
                Method = "assembly-info",
                Query = new string('a', DotsiderProtocol.MaxRequestBytes)
            },
            _testContext.CancellationToken);

        Assert.IsFalse(response.Success);
        Assert.Contains("byte limit", response.Error!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Exact-limit typed requests are written to the diagnostics socket.
    /// </summary>
    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public async Task SendAsync_ExactLimitRequest_Succeeds()
    {
        var socketPath = GetUniqueSocketPath();
        var actualByteCount = 0;
        await using var testSocket = new TestRawJsonSocket(socketPath);
        testSocket.OnRequest(requestJson =>
        {
            actualByteCount = Encoding.UTF8.GetByteCount(requestJson.GetRawText());
            return JsonSerializer.Serialize(
                DotsiderResponse.Ok(),
                DotsiderJsonContext.Protocol.Options);
        });
        testSocket.Start();
        var request = new DotsiderRequest
        {
            Method = "assembly-info",
            Query = ""
        };
        var baseline = JsonSerializer.Serialize(request, DotsiderJsonContext.Protocol.Options);
        request.Query = new string(
            'a',
            DotsiderProtocol.MaxRequestBytes - Encoding.UTF8.GetByteCount(baseline));
        var target = new RemoteDotsiderTarget(socketPath);

        var response = await target.SendAsync(
            request,
            _testContext.CancellationToken);

        Assert.IsTrue(response.Success, response.Error);
        Assert.AreEqual(DotsiderProtocol.MaxRequestBytes, actualByteCount);
    }

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
