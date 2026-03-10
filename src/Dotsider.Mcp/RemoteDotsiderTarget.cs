using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Dotsider.Core.Protocol;

namespace Dotsider.Mcp;

/// <summary>
/// Client for sending requests to a running dotsider instance via Unix domain socket.
/// </summary>
public sealed class RemoteDotsiderTarget(string socketPath)
{
    /// <summary>
    /// Sends a request and returns the deserialized response.
    /// </summary>
    public async Task<DotsiderResponse> SendAsync(
        DotsiderRequest request, CancellationToken ct = default)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.ReceiveTimeout = 10_000;
        socket.SendTimeout = 5_000;

        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

        await using var stream = new NetworkStream(socket, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        var requestJson = JsonSerializer.Serialize(request, DotsiderJsonOptions.Default);
        await writer.WriteLineAsync(requestJson.AsMemory(), ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var responseLine = await reader.ReadLineAsync(cts.Token);
        if (string.IsNullOrEmpty(responseLine))
            return DotsiderResponse.Fail("No response from socket");

        // Handle UTF-8 BOM if present
        if (responseLine.StartsWith('\uFEFF'))
            responseLine = responseLine[1..];

        return JsonSerializer.Deserialize<DotsiderResponse>(responseLine, DotsiderJsonOptions.Default)
            ?? DotsiderResponse.Fail("Invalid response");
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
        return JsonSerializer.Serialize(response.Data, DotsiderJsonOptions.Default);
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
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        await writer.WriteLineAsync(json.AsMemory(), ct);
        return await reader.ReadLineAsync(ct) ?? "";
    }
}
