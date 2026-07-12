using Dotsider.Core.Protocol;
using System.Net.Sockets;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Verifies deterministic lifetime behavior for the MCP test Unix domain socket servers.
/// </summary>
[TestClass]
public sealed class TestSocketLifecycleTests
{
    /// <summary>
    /// Gets or sets the current test context.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Verifies concurrent shutdown is idempotent while an accept is pending.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DotsiderSocket_ConcurrentShutdownWhileAccepting_IsIdempotent()
    {
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var server = new TestDotsiderSocket(GetUniqueSocketPath(), "test.dll");
            var socketPath = server.SocketPath;
            server.Start();

            var disposals = Enumerable.Range(0, 8)
                .Select(_ => server.DisposeAsync().AsTask())
                .ToArray();

            await Task.WhenAll(disposals);
            Assert.IsFalse(File.Exists(socketPath));
        }
    }

    /// <summary>
    /// Verifies a connection fault that precedes shutdown is surfaced by disposal.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DotsiderSocket_HandlerFaultBeforeShutdown_IsPropagated()
    {
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new TestDotsiderSocket(GetUniqueSocketPath(), "test.dll");
        server.OnMethod(
            "fail",
            _ =>
            {
                handlerEntered.SetResult();
                throw new InvalidOperationException("Expected handler failure.");
            });
        server.Start();

        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(
            new UnixDomainSocketEndPoint(server.SocketPath),
            TestContext.CancellationToken);
        await using var stream = new NetworkStream(client, ownsSocket: false);
        await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        var requestJson = JsonSerializer.Serialize(
            new DotsiderRequest { Method = "fail" },
            DotsiderJsonOptions.Default);
        await writer.WriteLineAsync(requestJson.AsMemory(), TestContext.CancellationToken);
        await handlerEntered.Task.WaitAsync(TestContext.CancellationToken);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => server.DisposeAsync().AsTask());
        Assert.AreEqual("Expected handler failure.", exception.Message);
        Assert.IsFalse(File.Exists(server.SocketPath));
    }

    /// <summary>
    /// Verifies shutdown never disguises a protocol handler fault as a transport teardown.
    /// </summary>
    /// <param name="faultKind">The shutdown-shaped handler exception to throw.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("ForeignCancellation")]
    [DataRow("ObjectDisposed")]
    public async Task DotsiderSocket_ShutdownShapedHandlerFault_IsPropagated(string faultKind)
    {
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseHandler = new ManualResetEventSlim();
        var server = new TestDotsiderSocket(GetUniqueSocketPath(), "test.dll");
        server.OnMethod(
            "fail",
            _ =>
            {
                handlerEntered.SetResult();
                releaseHandler.Wait(TestContext.CancellationToken);
                throw CreateShutdownShapedException(faultKind);
            });
        server.Start();

        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(
            new UnixDomainSocketEndPoint(server.SocketPath),
            TestContext.CancellationToken);
        await using var stream = new NetworkStream(client, ownsSocket: false);
        await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        var request = JsonSerializer.Serialize(
            new DotsiderRequest { Method = "fail" },
            DotsiderJsonOptions.Default);
        await writer.WriteLineAsync(request.AsMemory(), TestContext.CancellationToken);
        await handlerEntered.Task.WaitAsync(TestContext.CancellationToken);

        var disposal = server.DisposeAsync().AsTask();
        releaseHandler.Set();

        await AssertShutdownShapedExceptionAsync(faultKind, disposal);
        Assert.IsFalse(File.Exists(server.SocketPath));
    }

    /// <summary>
    /// Verifies shutdown never disguises a raw-JSON handler fault as a transport teardown.
    /// </summary>
    /// <param name="faultKind">The shutdown-shaped handler exception to throw.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("ForeignCancellation")]
    [DataRow("ObjectDisposed")]
    public async Task RawJsonSocket_ShutdownShapedHandlerFault_IsPropagated(string faultKind)
    {
        var socketPath = GetUniqueSocketPath();
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseHandler = new ManualResetEventSlim();
        var server = new TestRawJsonSocket(socketPath);
        server.OnRequest(
            _ =>
            {
                handlerEntered.SetResult();
                releaseHandler.Wait(TestContext.CancellationToken);
                throw CreateShutdownShapedException(faultKind);
            });
        server.Start();

        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(
            new UnixDomainSocketEndPoint(socketPath),
            TestContext.CancellationToken);
        await using var stream = new NetworkStream(client, ownsSocket: false);
        await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync("{}".AsMemory(), TestContext.CancellationToken);
        await handlerEntered.Task.WaitAsync(TestContext.CancellationToken);

        var disposal = server.DisposeAsync().AsTask();
        releaseHandler.Set();

        await AssertShutdownShapedExceptionAsync(faultKind, disposal);
        Assert.IsFalse(File.Exists(socketPath));
    }

    private static string GetUniqueSocketPath()
    {
        return Path.Combine(Path.GetTempPath(), $"mt-{Guid.NewGuid():N}");
    }

    private static Exception CreateShutdownShapedException(string faultKind) => faultKind switch
    {
        "ForeignCancellation" => new OperationCanceledException(
            "Expected foreign cancellation.",
            CancellationToken.None),
        "ObjectDisposed" => new ObjectDisposedException("handler", "Expected handler disposal fault."),
        _ => throw new ArgumentOutOfRangeException(nameof(faultKind)),
    };

    private static async Task AssertShutdownShapedExceptionAsync(string faultKind, Task disposal)
    {
        switch (faultKind)
        {
            case "ForeignCancellation":
                var cancellation = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                    () => disposal);
                Assert.AreEqual(CancellationToken.None, cancellation.CancellationToken);
                break;

            case "ObjectDisposed":
                var disposed = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(
                    () => disposal);
                Assert.AreEqual("handler", disposed.ObjectName);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(faultKind));
        }
    }
}
