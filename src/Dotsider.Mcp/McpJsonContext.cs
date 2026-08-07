using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotsider.Mcp;

/// <summary>
/// Provides source-generated JSON metadata for MCP application contracts.
/// Combines MCP-specific result types with the shared diagnostics protocol options.
/// Keeps the Native AOT server free from reflection-based serialization fallback.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DotsiderRequest))]
[JsonSerializable(typeof(DotsiderResponse))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(AssemblyResolution))]
[JsonSerializable(typeof(AssemblyDiffResult))]
[JsonSerializable(typeof(FrameworkAssemblyInfo))]
[JsonSerializable(typeof(ClrHeader))]
[JsonSerializable(typeof(CorrelationReport))]
[JsonSerializable(typeof(DgmlGraph))]
[JsonSerializable(typeof(IEnumerable<FieldDefInfo>))]
[JsonSerializable(typeof(IReadOnlyList<AssemblyRefInfo>))]
[JsonSerializable(typeof(IReadOnlyList<CustomAttributeInfo>))]
[JsonSerializable(typeof(IReadOnlyList<ResourceInfo>))]
[JsonSerializable(typeof(IReadOnlyList<SectionInfo>))]
[JsonSerializable(typeof(IReadOnlyList<TypeRefInfo>))]
[JsonSerializable(typeof(List<CustomAttributeInfo>))]
[JsonSerializable(typeof(List<MethodDebugInfo>))]
[JsonSerializable(typeof(List<RecoveredType>))]
[JsonSerializable(typeof(List<TypeDefInfo>))]
[JsonSerializable(typeof(NativeSymbolInfo))]
[JsonSerializable(typeof(PeHeaders))]
[JsonSerializable(typeof(ReadyToRunMethodReport))]
[JsonSerializable(typeof(ResolvedAssemblyInfo))]
[JsonSerializable(typeof(SizeNode))]
[JsonSerializable(typeof(SourceLinkInfo))]
internal sealed partial class McpJsonContext : JsonSerializerContext
{
    internal static McpJsonContext Application { get; } = new(DotsiderJsonContext.CreateOptions());
}
