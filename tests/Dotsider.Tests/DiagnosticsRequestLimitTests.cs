using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Verifies diagnostics request limits through a real Unix-domain socket listener.
/// </summary>
/// <param name="testContext">The context for the current test.</param>
[TestClass]
public sealed class DiagnosticsRequestLimitTests(TestContext testContext)
{
    private static readonly UTF8Encoding s_utf8NoBom =
        new(encoderShouldEmitUTF8Identifier: false);

    private readonly TestContext _testContext = testContext;

    /// <summary>
    /// Verifies an exact-limit JSON request is accepted through the real listener.
    /// </summary>
    [TestMethod]
    [Timeout(15_000, CooperativeCancellation = true)]
    public async Task Listener_ExactLimitRequest_Accepts()
    {
        await using var listener = StartListener();
        var json = CreateExactLimitRequest();

        var response = await SendBytesAsync(
            listener.SocketPath!,
            Encoding.UTF8.GetBytes(json + "\r\n"),
            shutdownSend: false,
            _testContext.CancellationToken);

        Assert.IsTrue(response.Success, response.Error);
    }

    /// <summary>
    /// Verifies an oversized request is rejected and its connection slot is released.
    /// </summary>
    [TestMethod]
    [Timeout(15_000, CooperativeCancellation = true)]
    public async Task Listener_OneByteOverLimit_RejectsAndRecovers()
    {
        await using var listener = StartListener();
        var oversized = Encoding.UTF8.GetBytes(CreateExactLimitRequest() + "x\n");

        var rejected = await SendBytesAsync(
            listener.SocketPath!,
            oversized,
            shutdownSend: false,
            _testContext.CancellationToken);
        var recovered = await DotsiderClient.SendAsync(
            listener.SocketPath!,
            new DotsiderRequest { Method = "assembly-info" },
            _testContext.CancellationToken);

        Assert.IsFalse(rejected.Success);
        Assert.Contains("byte limit", rejected.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(DotsiderProtocol.Version, rejected.V);
        Assert.IsTrue(recovered.Success, recovered.Error);
    }

    /// <summary>
    /// Verifies malformed UTF-8 receives a versioned failure response.
    /// </summary>
    [TestMethod]
    [Timeout(15_000, CooperativeCancellation = true)]
    public async Task Listener_InvalidUtf8_Rejects()
    {
        await using var listener = StartListener();
        byte[] bytes = [0xC3, 0x28, (byte)'\n'];

        var response = await SendBytesAsync(
            listener.SocketPath!,
            bytes,
            shutdownSend: false,
            _testContext.CancellationToken);

        Assert.IsFalse(response.Success);
        Assert.Contains("UTF-8", response.Error!);
        Assert.AreEqual(DotsiderProtocol.Version, response.V);
    }

    /// <summary>
    /// Verifies existing BOM-emitting clients remain compatible.
    /// </summary>
    [TestMethod]
    [Timeout(15_000, CooperativeCancellation = true)]
    public async Task Listener_Utf8Bom_Accepts()
    {
        await using var listener = StartListener();
        var json = JsonSerializer.Serialize(
            new DotsiderRequest { Method = "assembly-info" },
            DotsiderJsonContext.Protocol.Options);
        var payload = new byte[s_utf8NoBom.GetByteCount(json) + 4];
        payload[0] = 0xEF;
        payload[1] = 0xBB;
        payload[2] = 0xBF;
        s_utf8NoBom.GetBytes(json, payload.AsSpan(3));
        payload[^1] = (byte)'\n';

        var response = await SendBytesAsync(
            listener.SocketPath!,
            payload,
            shutdownSend: false,
            _testContext.CancellationToken);

        Assert.IsTrue(response.Success, response.Error);
    }

    /// <summary>
    /// Verifies EOF can terminate the existing single-request connection contract.
    /// </summary>
    [TestMethod]
    [Timeout(15_000, CooperativeCancellation = true)]
    public async Task Listener_EofTerminatedRequest_Accepts()
    {
        await using var listener = StartListener();
        var json = JsonSerializer.Serialize(
            new DotsiderRequest { Method = "assembly-info" },
            DotsiderJsonContext.Protocol.Options);

        var response = await SendBytesAsync(
            listener.SocketPath!,
            s_utf8NoBom.GetBytes(json),
            shutdownSend: true,
            _testContext.CancellationToken);

        Assert.IsTrue(response.Success, response.Error);
    }

    /// <summary>
    /// Verifies typed requests fail locally before attempting a socket connection.
    /// </summary>
    [TestMethod]
    public async Task DotsiderClient_OversizedTypedRequest_FailsBeforeConnect()
    {
        var response = await DotsiderClient.SendAsync(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"),
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
    /// Verifies the typed client accepts a request whose serialized UTF-8 payload
    /// is exactly at the protocol limit.
    /// </summary>
    [TestMethod]
    [Timeout(15_000, CooperativeCancellation = true)]
    public async Task DotsiderClient_ExactLimitTypedRequest_Accepts()
    {
        await using var listener = StartListener();
        var request = JsonSerializer.Deserialize<DotsiderRequest>(
            CreateExactLimitRequest(),
            DotsiderJsonContext.Protocol.Options);
        Assert.IsNotNull(request);

        var response = await DotsiderClient.SendAsync(
            listener.SocketPath!,
            request,
            _testContext.CancellationToken);

        Assert.IsTrue(response.Success, response.Error);
    }

    /// <summary>
    /// Verifies the raw transport used by Hex1b does not inherit the Dotsider request cap.
    /// </summary>
    [TestMethod]
    [Timeout(15_000, CooperativeCancellation = true)]
    public async Task DotsiderClient_RawTransport_DoesNotApplyDotsiderLimit()
    {
        var socketPath = Path.Combine(Path.GetTempPath(), $"raw-{Guid.NewGuid():N}");
        await using var socket = new TestRawJsonSocket(socketPath);
        socket.OnRequest(static _ => """{"accepted":true}""");
        socket.Start();
        var json = JsonSerializer.Serialize(new
        {
            payload = new string('a', DotsiderProtocol.MaxRequestBytes)
        });

        var response = await DotsiderClient.SendRawAsync(
            socketPath,
            json,
            _testContext.CancellationToken);

        Assert.AreEqual("""{"accepted":true}""", response);
    }

    private static string CreateExactLimitRequest()
    {
        var request = new DotsiderRequest
        {
            Method = "assembly-info",
            Query = ""
        };
        var baseline = JsonSerializer.Serialize(request, DotsiderJsonContext.Protocol.Options);
        var paddingLength = DotsiderProtocol.MaxRequestBytes
            - Encoding.UTF8.GetByteCount(baseline);
        Assert.IsGreaterThan(0, paddingLength);

        request.Query = new string('a', paddingLength);
        var result = JsonSerializer.Serialize(request, DotsiderJsonContext.Protocol.Options);
        Assert.AreEqual(
            DotsiderProtocol.MaxRequestBytes,
            Encoding.UTF8.GetByteCount(result));
        return result;
    }

    private static DotsiderDiagnosticsListener StartListener()
    {
        var listener = new DotsiderDiagnosticsListener(
            static () => null,
            assemblyInfoProvider: static () => TestJsonResponse.Element(new
            {
                FileName = "sample.dll"
            }));
        listener.StartListening(TestSocketIds.NextPid());
        return listener;
    }

    private static async Task<DotsiderResponse> SendBytesAsync(
        string socketPath,
        byte[] payload,
        bool shutdownSend,
        CancellationToken cancellationToken)
    {
        using var socket = new Socket(
            AddressFamily.Unix,
            SocketType.Stream,
            ProtocolType.Unspecified);
        await socket.ConnectAsync(
            new UnixDomainSocketEndPoint(socketPath),
            cancellationToken);
        await using var stream = new NetworkStream(socket, ownsSocket: false);

        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        if (shutdownSend)
        {
            socket.Shutdown(SocketShutdown.Send);
        }

        using var reader = new StreamReader(stream, s_utf8NoBom, leaveOpen: true);
        var line = await reader.ReadLineAsync(cancellationToken);
        Assert.IsNotNull(line);
        var response = JsonSerializer.Deserialize<DotsiderResponse>(
            line,
            DotsiderJsonContext.Protocol.Options);
        Assert.IsNotNull(response);
        return response;
    }
}
