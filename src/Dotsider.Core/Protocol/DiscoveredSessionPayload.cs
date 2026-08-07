using System.Text.Json;

namespace Dotsider.Core.Protocol;

/// <summary>
/// A live dotsider session discovered over its diagnostics socket.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record DiscoveredSessionPayload(int Pid, string SocketPath, JsonElement? Info);
