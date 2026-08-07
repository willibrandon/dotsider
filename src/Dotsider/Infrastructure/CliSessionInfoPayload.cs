using System.Text.Json;

namespace Dotsider.Infrastructure;

/// <summary>
/// Assembly and view state for a live dotsider session.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliSessionInfoPayload(JsonElement? AssemblyInfo, JsonElement? CurrentView);
