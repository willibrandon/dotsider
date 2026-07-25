using Dotsider.Core.Protocol;
using Dotsider.Diagnostics;
using System.Text;

namespace Dotsider.Tests;

/// <summary>
/// Verifies byte-bounded UTF-8 diagnostics line framing.
/// </summary>
[TestClass]
public sealed class BoundedUtf8LineReaderTests
{
    /// <summary>
    /// Verifies exact-size payloads accept LF, CRLF, and EOF framing.
    /// </summary>
    /// <param name="suffix">The framing bytes after the payload.</param>
    [TestMethod]
    [DataRow("\n")]
    [DataRow("\r\n")]
    [DataRow("")]
    public async Task ReadAsync_ExactLimit_AcceptsSupportedFraming(string suffix)
    {
        var payload = new string('a', DotsiderProtocol.MaxRequestBytes);
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(payload + suffix));

        var result = await BoundedUtf8LineReader.ReadAsync(
            stream,
            DotsiderProtocol.MaxRequestBytes,
            CancellationToken.None);

        Assert.AreEqual(BoundedUtf8LineReadStatus.Success, result.Status);
        Assert.IsNotNull(result.Value);
        Assert.HasCount(DotsiderProtocol.MaxRequestBytes, result.Value);
    }

    /// <summary>
    /// Verifies the first byte beyond the payload limit is rejected.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_OneByteOverLimit_Rejects()
    {
        var payload = new string('a', DotsiderProtocol.MaxRequestBytes + 1);
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(payload + "\n"));

        var result = await BoundedUtf8LineReader.ReadAsync(
            stream,
            DotsiderProtocol.MaxRequestBytes,
            CancellationToken.None);

        Assert.AreEqual(BoundedUtf8LineReadStatus.TooLarge, result.Status);
        Assert.IsNull(result.Value);
    }

    /// <summary>
    /// Verifies the limit measures encoded bytes rather than UTF-16 characters.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_MultibytePayload_EnforcesUtf8Bytes()
    {
        var exactPayload = new string('é', DotsiderProtocol.MaxRequestBytes / 2);
        await using var exactStream = new MemoryStream(
            Encoding.UTF8.GetBytes(exactPayload + "\n"));

        var exactResult = await BoundedUtf8LineReader.ReadAsync(
            exactStream,
            DotsiderProtocol.MaxRequestBytes,
            CancellationToken.None);

        Assert.AreEqual(BoundedUtf8LineReadStatus.Success, exactResult.Status);
        Assert.IsNotNull(exactResult.Value);
        Assert.AreEqual(exactPayload.Length, exactResult.Value.Length);

        await using var oversizedStream = new MemoryStream(
            Encoding.UTF8.GetBytes(exactPayload + "é\n"));
        var oversizedResult = await BoundedUtf8LineReader.ReadAsync(
            oversizedStream,
            DotsiderProtocol.MaxRequestBytes,
            CancellationToken.None);

        Assert.AreEqual(BoundedUtf8LineReadStatus.TooLarge, oversizedResult.Status);
    }

    /// <summary>
    /// Verifies an optional UTF-8 byte-order mark and CRLF framing are accepted
    /// around an exact-limit payload.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_ExactLimitWithUtf8BomAndCrLf_Accepts()
    {
        var bytes = new byte[DotsiderProtocol.MaxRequestBytes + 5];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        bytes.AsSpan(3, DotsiderProtocol.MaxRequestBytes).Fill((byte)'a');
        bytes[^2] = (byte)'\r';
        bytes[^1] = (byte)'\n';
        await using var stream = new MemoryStream(bytes);

        var result = await BoundedUtf8LineReader.ReadAsync(
            stream,
            DotsiderProtocol.MaxRequestBytes,
            CancellationToken.None);

        Assert.AreEqual(BoundedUtf8LineReadStatus.Success, result.Status);
        Assert.IsNotNull(result.Value);
        Assert.HasCount(DotsiderProtocol.MaxRequestBytes, result.Value);
    }

    /// <summary>
    /// Verifies invalid UTF-8 never reaches JSON deserialization.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_InvalidUtf8_Rejects()
    {
        byte[] bytes = [0xC3, 0x28, (byte)'\n'];
        await using var stream = new MemoryStream(bytes);

        var result = await BoundedUtf8LineReader.ReadAsync(
            stream,
            DotsiderProtocol.MaxRequestBytes,
            CancellationToken.None);

        Assert.AreEqual(BoundedUtf8LineReadStatus.InvalidUtf8, result.Status);
        Assert.IsNull(result.Value);
    }

    /// <summary>
    /// Verifies an empty stream is distinguished from an empty request line.
    /// </summary>
    [TestMethod]
    public async Task ReadAsync_EmptyStream_ReturnsEndOfStream()
    {
        await using var stream = new MemoryStream();

        var result = await BoundedUtf8LineReader.ReadAsync(
            stream,
            DotsiderProtocol.MaxRequestBytes,
            CancellationToken.None);

        Assert.AreEqual(BoundedUtf8LineReadStatus.EndOfStream, result.Status);
    }
}
