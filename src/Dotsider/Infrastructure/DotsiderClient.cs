using System.Net.Sockets;
using System.Text.Json;
using Dotsider.Core.Protocol;

namespace Dotsider.Infrastructure;

/// <summary>
/// Client for communicating with a dotsider diagnostics socket.
/// Sends a single JSON request, receives a single JSON response, then closes the connection.
/// </summary>
internal sealed class DotsiderClient
{
    /// <summary>
    /// Sends a DotsiderRequest to the specified socket and returns the response.
    /// </summary>
    public async Task<DotsiderResponse> SendAsync(
        string socketPath, DotsiderRequest request, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(request, DotsiderJsonOptions.Default);
        var responseJson = await SendRawAsync(socketPath, json, ct);
        return JsonSerializer.Deserialize<DotsiderResponse>(responseJson, DotsiderJsonOptions.Default)
            ?? new DotsiderResponse { Success = false, Error = "Empty response" };
    }

    /// <summary>
    /// Sends a raw JSON string to the specified socket and returns the raw response.
    /// Used for both dotsider and hex1b protocol interactions.
    /// </summary>
    public async Task<string> SendRawAsync(
        string socketPath, string json, CancellationToken ct = default)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct);

        await using var stream = new NetworkStream(socket, ownsSocket: false);
        await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(stream, leaveOpen: true);

        await writer.WriteLineAsync(json.AsMemory(), ct);
        return await reader.ReadLineAsync(ct) ?? "";
    }

    /// <summary>
    /// Probes a dotsider socket to check if the instance is reachable.
    /// Returns the assembly-info response, or null if unreachable.
    /// </summary>
    public async Task<DotsiderResponse?> TryProbeAsync(
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
