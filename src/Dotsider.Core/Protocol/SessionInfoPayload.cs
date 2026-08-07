using System.Text.Json;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Assembly and view state returned for one live session.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record SessionInfoPayload(JsonElement? Assembly, JsonElement? View);
