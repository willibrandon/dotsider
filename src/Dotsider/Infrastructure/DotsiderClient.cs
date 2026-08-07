using Dotsider.Core.Protocol;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Dotsider.Infrastructure;

/// <summary>
/// Client for communicating with a dotsider diagnostics socket.
/// Sends a single JSON request, receives a single JSON response, then closes the connection.
/// </summary>
internal sealed class DotsiderClient
{
    private static readonly UTF8Encoding s_utf8NoBom =
        new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Sends a DotsiderRequest to the specified socket and returns the response.
    /// </summary>
    public static async Task<DotsiderResponse> SendAsync(
        string socketPath, DotsiderRequest request, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(request, DotsiderJsonContext.Protocol.DotsiderRequest);
        var requestBytes = Encoding.UTF8.GetByteCount(json);
        if (requestBytes > DotsiderProtocol.MaxRequestBytes)
        {
            return DotsiderResponse.Fail(
                $"Request is {requestBytes} bytes and exceeds the " +
                $"{DotsiderProtocol.MaxRequestBytes}-byte limit");
        }

        var responseJson = await SendRawAsync(socketPath, json, ct);

        DotsiderResponse response;
        try
        {
            response = JsonSerializer.Deserialize(responseJson, DotsiderJsonContext.Protocol.DotsiderResponse)
                ?? new DotsiderResponse { Success = false, Error = "Empty response" };
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
    /// Sends a raw JSON string to the specified socket and returns the raw response.
    /// Used for both dotsider and hex1b protocol interactions.
    /// </summary>
    public static async Task<string> SendRawAsync(
        string socketPath, string json, CancellationToken ct = default)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

        await using var stream = new NetworkStream(socket, ownsSocket: false);
        await using var writer = new StreamWriter(stream, s_utf8NoBom, leaveOpen: true)
        {
            AutoFlush = true
        };
        using var reader = new StreamReader(stream, s_utf8NoBom, leaveOpen: true);

        await writer.WriteLineAsync(json.AsMemory(), ct);
        return await reader.ReadLineAsync(ct) ?? "";
    }

    /// <summary>
    /// Probes a dotsider socket to check if the instance is reachable.
    /// Returns the assembly-info response, or null if unreachable.
    /// </summary>
    public static async Task<DotsiderResponse?> TryProbeAsync(
        string socketPath, CancellationToken ct = default)
    {
        try
        {
            return await SendAsync(socketPath,
                new DotsiderRequest { Method = "assembly-info" }, ct);
        }
        catch
        {
            return null;
        }
    }
}
