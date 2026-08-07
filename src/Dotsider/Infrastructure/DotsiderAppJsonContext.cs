using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotsider.Infrastructure;

/// <summary>
/// Provides source-generated JSON metadata for CLI and session contracts.
/// Registers application payloads that are not part of the shared protocol.
/// Keeps command output serialization compatible with Native AOT compilation.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DotsiderRequest))]
[JsonSerializable(typeof(DotsiderResponse))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(IReadOnlyList<FieldDefInfo>))]
[JsonSerializable(typeof(IReadOnlyList<MethodDefInfo>))]
[JsonSerializable(typeof(IReadOnlyList<RecoveredType>))]
[JsonSerializable(typeof(IReadOnlyList<TypeDefInfo>))]
[JsonSerializable(typeof(NuGetSessionAssemblyPayload))]
[JsonSerializable(typeof(NuGetSessionViewPayload))]
[JsonSerializable(typeof(SizeDiffSessionAssemblyPayload))]
[JsonSerializable(typeof(SizeDiffSessionViewPayload))]
[JsonSerializable(typeof(DiffSessionAssemblyPayload))]
[JsonSerializable(typeof(DiffSessionViewPayload))]
[JsonSerializable(typeof(CliAssemblyInfoPayload))]
[JsonSerializable(typeof(CliDependenciesPayload))]
[JsonSerializable(typeof(CliNativeSymbolsPayload))]
[JsonSerializable(typeof(CliWhyPayload))]
[JsonSerializable(typeof(CliCorrelationSummaryPayload))]
[JsonSerializable(typeof(CliAmbiguityPayload))]
[JsonSerializable(typeof(CliReadyToRunPayload))]
[JsonSerializable(typeof(CliSessionInfoPayload))]
[JsonSerializable(typeof(CliCapturePayload))]
[JsonSerializable(typeof(CliPathPayload))]
[JsonSerializable(typeof(CliSizeReportPayload))]
[JsonSerializable(typeof(IlDisassemblyPayload))]
[JsonSerializable(typeof(NativeDisassemblyPayload))]
[JsonSerializable(typeof(StringsPayload))]
[JsonSerializable(typeof(List<CliDiscoveredSessionPayload>))]
[JsonSerializable(typeof(EmbeddedSourceInfo))]
[JsonSerializable(typeof(SizeNode))]
[JsonSerializable(typeof(CorrelationReport))]
[JsonSerializable(typeof(ReadyToRunMethodReport))]
[JsonSerializable(typeof(IReadOnlyList<FieldDefInfo>))]
[JsonSerializable(typeof(BundleManifest))]
internal sealed partial class DotsiderAppJsonContext : JsonSerializerContext
{
    internal static DotsiderAppJsonContext Application { get; } =
        new(DotsiderJsonContext.CreateOptions());

    internal static JsonElement SerializeToElement<T>(T value)
    {
        var typeInfo = Application.GetTypeInfo(typeof(T))
            ?? throw new InvalidOperationException(
                $"No source-generated JSON metadata is registered for {typeof(T)}.");
        return JsonSerializer.SerializeToElement(value, typeInfo);
    }
}
