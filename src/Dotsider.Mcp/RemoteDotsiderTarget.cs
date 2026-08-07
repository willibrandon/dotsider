using Dotsider.Core.Protocol;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Dotsider.Mcp;

/// <summary>
/// Client for sending requests to a running dotsider instance via Unix domain socket.
/// </summary>
public sealed class RemoteDotsiderTarget(string socketPath)
{
    private static readonly UTF8Encoding s_utf8NoBom =
        new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Sends a request and returns the deserialized response.
    /// </summary>
    public async Task<DotsiderResponse> SendAsync(
        DotsiderRequest request, CancellationToken ct = default)
    {
        var requestJson = JsonSerializer.Serialize(request, DotsiderJsonContext.Protocol.DotsiderRequest);
        var requestBytes = Encoding.UTF8.GetByteCount(requestJson);
        if (requestBytes > DotsiderProtocol.MaxRequestBytes)
        {
            return DotsiderResponse.Fail(
                $"Request is {requestBytes} bytes and exceeds the " +
                $"{DotsiderProtocol.MaxRequestBytes}-byte limit");
        }

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.ReceiveTimeout = 10_000;
        socket.SendTimeout = 5_000;

        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

        await using var stream = new NetworkStream(socket, ownsSocket: false);
        using var reader = new StreamReader(stream, s_utf8NoBom);
        await using var writer = new StreamWriter(stream, s_utf8NoBom)
        {
            AutoFlush = true
        };

        await writer.WriteLineAsync(requestJson.AsMemory(), ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var responseLine = await reader.ReadLineAsync(cts.Token);
        if (string.IsNullOrEmpty(responseLine))
            return DotsiderResponse.Fail("No response from socket");

        // Handle UTF-8 BOM if present
        if (responseLine.StartsWith('\uFEFF'))
            responseLine = responseLine[1..];

        DotsiderResponse response;
        try
        {
            response = JsonSerializer.Deserialize(responseLine, DotsiderJsonContext.Protocol.DotsiderResponse)
                ?? DotsiderResponse.Fail("Invalid response");
        }
        catch (JsonException ex)
        {
            return DotsiderResponse.Fail($"Invalid server response: {ex.Message}");
        }

        if (response.V != DotsiderProtocol.Version)
            return DotsiderResponse.Fail(
                $"Server protocol version mismatch: expected {DotsiderProtocol.Version}, got {response.V}");

        return response;
    }

    /// <summary>
    /// Sends a request and returns the unwrapped data as a JSON string.
    /// If the response indicates failure, returns an error message.
    /// This ensures session-backed tools return the same JSON shape as direct-analysis tools.
    /// </summary>
    public async Task<string> SendAndUnwrapAsync(
        DotsiderRequest request, CancellationToken ct = default)
    {
        var response = await SendAsync(request, ct);
        if (!response.Success)
            return $"Error: {response.Error ?? "Unknown error"}";
        return response.Data?.GetRawText() ?? "null";
    }

    /// <summary>
    /// Sends a raw JSON string (for hex1b protocol interaction).
    /// </summary>
    public static async Task<string> SendRawAsync(
        string socketPath, string json, CancellationToken ct = default)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

        await using var stream = new NetworkStream(socket, ownsSocket: false);
        using var reader = new StreamReader(stream, s_utf8NoBom);
        await using var writer = new StreamWriter(stream, s_utf8NoBom)
        {
            AutoFlush = true
        };

        await writer.WriteLineAsync(json.AsMemory(), ct);
        return await reader.ReadLineAsync(ct) ?? "";
    }
}
