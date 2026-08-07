using Dotsider.Core.Analysis.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Source-generated JSON metadata for the dotsider diagnostics protocol.
/// Registers every supported request, response, payload, and model contract.
/// Supplies reflection-free serialization metadata to Native AOT applications.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DotsiderRequest))]
[JsonSerializable(typeof(DotsiderResponse))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(AssemblyDiffResult))]
[JsonSerializable(typeof(BundleManifest))]
[JsonSerializable(typeof(IReadOnlyList<AssemblyRefInfo>))]
[JsonSerializable(typeof(IReadOnlyList<CounterSnapshot>))]
[JsonSerializable(typeof(IReadOnlyList<CustomAttributeInfo>))]
[JsonSerializable(typeof(IReadOnlyList<FieldDefInfo>))]
[JsonSerializable(typeof(IReadOnlyList<MethodDefInfo>))]
[JsonSerializable(typeof(IReadOnlyList<OutputLine>))]
[JsonSerializable(typeof(IReadOnlyList<RecoveredType>))]
[JsonSerializable(typeof(IReadOnlyList<ResourceInfo>))]
[JsonSerializable(typeof(IReadOnlyList<SectionInfo>))]
[JsonSerializable(typeof(IReadOnlyList<TraceEventEntry>))]
[JsonSerializable(typeof(IReadOnlyList<TypeDefInfo>))]
[JsonSerializable(typeof(IReadOnlyList<TypeRefInfo>))]
[JsonSerializable(typeof(List<MethodDefInfo>))]
[JsonSerializable(typeof(List<RecoveredMethodPayload>))]
[JsonSerializable(typeof(List<MstatLargestMethodPayload>))]
[JsonSerializable(typeof(List<NativeSymbolLargestMethodPayload>))]
[JsonSerializable(typeof(List<IlLargestMethodPayload>))]
[JsonSerializable(typeof(ClrHeader))]
[JsonSerializable(typeof(CorrelationReport))]
[JsonSerializable(typeof(MethodDebugInfo))]
[JsonSerializable(typeof(NativeSymbolInfo))]
[JsonSerializable(typeof(PeHeaders))]
[JsonSerializable(typeof(ReadyToRunMethodReport))]
[JsonSerializable(typeof(SizeNode))]
[JsonSerializable(typeof(SourceLinkInfo))]
[JsonSerializable(typeof(TraceSummary))]
[JsonSerializable(typeof(MetadataMemberSearchPayload))]
[JsonSerializable(typeof(RecoveredMemberSearchPayload))]
[JsonSerializable(typeof(NativeAotInfoPayload))]
[JsonSerializable(typeof(NativeAotSectionsPayload))]
[JsonSerializable(typeof(MstatContributorsPayload))]
[JsonSerializable(typeof(MstatWhyPayload))]
[JsonSerializable(typeof(SizeDiffPayload))]
[JsonSerializable(typeof(SizeBudgetPayload))]
[JsonSerializable(typeof(WasmSummary))]
[JsonSerializable(typeof(WasmSectionsPayload))]
[JsonSerializable(typeof(WasmFunctionsPayload))]
[JsonSerializable(typeof(WebcilSummary))]
[JsonSerializable(typeof(AssemblyInfoPayload))]
[JsonSerializable(typeof(IlDisassemblyPayload))]
[JsonSerializable(typeof(List<IlSearchResultPayload>))]
[JsonSerializable(typeof(StringsPayload))]
[JsonSerializable(typeof(NativeDisassemblyPayload))]
[JsonSerializable(typeof(NativeSymbolAmbiguityPayload))]
[JsonSerializable(typeof(ReadyToRunAmbiguityPayload))]
[JsonSerializable(typeof(NuGetPackagePayload))]
[JsonSerializable(typeof(BundleInfoPayload))]
[JsonSerializable(typeof(List<BundleEntryPayload>))]
[JsonSerializable(typeof(List<DiscoveredSessionPayload>))]
[JsonSerializable(typeof(SessionInfoPayload))]
[JsonSerializable(typeof(TokenResolutionPayload))]
[JsonSerializable(typeof(DependencyGraphPayload))]
[JsonSerializable(typeof(MessagePayload))]
[JsonSerializable(typeof(OperationStatusPayload))]
[JsonSerializable(typeof(BundleProbePayload))]
[JsonSerializable(typeof(ByteRangePayload))]
[JsonSerializable(typeof(CurrentViewPayload))]
[JsonSerializable(typeof(ResolvedAssemblyInfo))]
public sealed partial class DotsiderJsonContext : JsonSerializerContext
{
    /// <summary>
    /// The protocol context configured with camel-case enum values.
    /// </summary>
    public static DotsiderJsonContext Protocol { get; } = new(CreateOptions());

    /// <summary>
    /// Creates the JSON options shared by all dotsider source-generated contexts.
    /// </summary>
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter<AssemblyProvenance>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<BinaryKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<BundleFileType>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<CorrelationQueryOutcome>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<DiffKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<GraphNodeKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<MemberRefKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<MethodCorrelationStatus>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<MstatSectionKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<NativeArchitecture>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<NativeFlowKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<NativeInstructionCategory>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<NativeOperandKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<NativeTargetKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<NativeSymbolStatus>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<NativeSymbolSource>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<NativeSymbolKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<PdbProvenanceKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<PreIlcPdbStatus>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<PreIlcAssemblyOrigin>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<ReadyToRunStatus>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<ReadyToRunSectionType>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<ReadyToRunCodeRangeKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<ReadyToRunQueryOutcome>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<ReadyToRunNativeAvailability>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<SizeBasis>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<SizeBudgetMetric>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<SizeBudgetSeverity>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<SizeBudgetScope>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<SizeNodeKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<StringSource>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<TraceEventCategory>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<TraceProcessState>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<WasmExternalKind>(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new JsonStringEnumConverter<WasmSymbolMapStatus>(JsonNamingPolicy.CamelCase));
        return options;
    }
}
