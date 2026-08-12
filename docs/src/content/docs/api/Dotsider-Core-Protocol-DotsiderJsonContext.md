---
title: "DotsiderJsonContext"
description: "Source-generated JSON metadata for the dotsider diagnostics protocol. Registers every supported request, response, payload, and model contract. Supplies reflection-free serialization metadata to Native AOT applications."
slug: api/dotsider.core.protocol.dotsiderjsoncontext
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Source-generated JSON metadata for the dotsider diagnostics protocol.
Registers every supported request, response, payload, and model contract.
Supplies reflection-free serialization metadata to Native AOT applications.

```csharp
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
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
public sealed class DotsiderJsonContext : JsonSerializerContext, IJsonTypeInfoResolver
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [JsonSerializerContext](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonserializercontext) → **DotsiderJsonContext**

## Implements

- [IJsonTypeInfoResolver](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.ijsontypeinforesolver)

## Constructors

### DotsiderJsonContext()

```csharp
public DotsiderJsonContext()
```

### DotsiderJsonContext(JsonSerializerOptions)

**Parameters:**

- `options` ([JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions))

```csharp
public DotsiderJsonContext(JsonSerializerOptions options)
```

## Properties

### AssemblyDiffResult

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<AssemblyDiffResult\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<AssemblyDiffResult> AssemblyDiffResult { get; }
```

### AssemblyInfoPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<AssemblyInfoPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<AssemblyInfoPayload> AssemblyInfoPayload { get; }
```

### AssemblyRefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<AssemblyRefInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<AssemblyRefInfo> AssemblyRefInfo { get; }
```

### BinaryKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<BinaryKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<BinaryKind> BinaryKind { get; }
```

### Boolean

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Boolean\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<bool> Boolean { get; }
```

### BundleEntry

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<BundleEntry\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<BundleEntry> BundleEntry { get; }
```

### BundleEntryPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<BundleEntryPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<BundleEntryPayload> BundleEntryPayload { get; }
```

### BundleFileType

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<BundleFileType\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<BundleFileType> BundleFileType { get; }
```

### BundleInfoPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<BundleInfoPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<BundleInfoPayload> BundleInfoPayload { get; }
```

### BundleManifest

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<BundleManifest\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<BundleManifest> BundleManifest { get; }
```

### BundleProbePayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<BundleProbePayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<BundleProbePayload> BundleProbePayload { get; }
```

### Byte

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Byte\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<byte> Byte { get; }
```

### ByteRangePayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<ByteRangePayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ByteRangePayload> ByteRangePayload { get; }
```

### Characteristics

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Characteristics\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<Characteristics> Characteristics { get; }
```

### ClrHeader

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<ClrHeader\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ClrHeader> ClrHeader { get; }
```

### CorFlags

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CorFlags\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<CorFlags> CorFlags { get; }
```

### CorrelationCandidate

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<CorrelationCandidate> CorrelationCandidate { get; }
```

### CorrelationReport

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CorrelationReport\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<CorrelationReport> CorrelationReport { get; }
```

### CorrelationReportSymbol

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CorrelationReportSymbol\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<CorrelationReportSymbol> CorrelationReportSymbol { get; }
```

### CounterSnapshot

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CounterSnapshot\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<CounterSnapshot> CounterSnapshot { get; }
```

### CurrentViewPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CurrentViewPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<CurrentViewPayload> CurrentViewPayload { get; }
```

### CustomAttributeInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CustomAttributeInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<CustomAttributeInfo> CustomAttributeInfo { get; }
```

### Default

The default [JsonSerializerContext](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.jsonserializercontext) associated with a default [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions) instance.

**Returns:** [DotsiderJsonContext](/api/dotsider.core.protocol.dotsiderjsoncontext/)

```csharp
public static DotsiderJsonContext Default { get; }
```

### DependencyGraphPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DependencyGraphPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DependencyGraphPayload> DependencyGraphPayload { get; }
```

### DgmlPathStep

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DgmlPathStep\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DgmlPathStep> DgmlPathStep { get; }
```

### DiffEntryAssemblyRefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<AssemblyRefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DiffEntry<AssemblyRefInfo>> DiffEntryAssemblyRefInfo { get; }
```

### DiffEntryMethodDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MethodDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DiffEntry<MethodDefInfo>> DiffEntryMethodDefInfo { get; }
```

### DiffEntryTypeDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TypeDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DiffEntry<TypeDefInfo>> DiffEntryTypeDefInfo { get; }
```

### DiffKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DiffKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DiffKind> DiffKind { get; }
```

### DiffSummary

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DiffSummary\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DiffSummary> DiffSummary { get; }
```

### DirectoryEntry

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DirectoryEntry\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DirectoryEntry> DirectoryEntry { get; }
```

### DiscoveredSessionPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DiscoveredSessionPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DiscoveredSessionPayload> DiscoveredSessionPayload { get; }
```

### DllCharacteristics

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DllCharacteristics\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DllCharacteristics> DllCharacteristics { get; }
```

### DotsiderRequest

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DotsiderRequest\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DotsiderRequest> DotsiderRequest { get; }
```

### DotsiderResponse

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DotsiderResponse\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<DotsiderResponse> DotsiderResponse { get; }
```

### Double

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Double\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<double> Double { get; }
```

### FieldAttributes

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<FieldAttributes\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<FieldAttributes> FieldAttributes { get; }
```

### FieldDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<FieldDefInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<FieldDefInfo> FieldDefInfo { get; }
```

### GeneratedSerializerOptions

The source-generated options associated with this context.

**Returns:** [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

```csharp
protected override JsonSerializerOptions? GeneratedSerializerOptions { get; }
```

### GraphEdge

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<GraphEdge\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<GraphEdge> GraphEdge { get; }
```

### GraphNode

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<GraphNode\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<GraphNode> GraphNode { get; }
```

### GraphNodeKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<GraphNodeKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<GraphNodeKind> GraphNodeKind { get; }
```

### Guid

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Guid\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<Guid> Guid { get; }
```

### IlDisassemblyPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<IlDisassemblyPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IlDisassemblyPayload> IlDisassemblyPayload { get; }
```

### IlInstruction

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IlInstruction> IlInstruction { get; }
```

### IlLargestMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<IlLargestMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IlLargestMethodPayload> IlLargestMethodPayload { get; }
```

### IlSearchResultPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<IlSearchResultPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IlSearchResultPayload> IlSearchResultPayload { get; }
```

### Int32

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Int32\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<int> Int32 { get; }
```

### Int64

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Int64\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<long> Int64 { get; }
```

### IReadOnlyDictionaryTraceEventCategoryInt32

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TraceEventCategory, Int32\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-2)

```csharp
public JsonTypeInfo<IReadOnlyDictionary<TraceEventCategory, int>> IReadOnlyDictionaryTraceEventCategoryInt32 { get; }
```

### IReadOnlyListAssemblyRefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<AssemblyRefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<AssemblyRefInfo>> IReadOnlyListAssemblyRefInfo { get; }
```

### IReadOnlyListBundleEntry

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<BundleEntry\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<BundleEntry>> IReadOnlyListBundleEntry { get; }
```

### IReadOnlyListByte

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Byte\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<byte>> IReadOnlyListByte { get; }
```

### IReadOnlyListCorrelationCandidate

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CorrelationCandidate\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<CorrelationCandidate>> IReadOnlyListCorrelationCandidate { get; }
```

### IReadOnlyListCorrelationReportSymbol

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CorrelationReportSymbol\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<CorrelationReportSymbol>> IReadOnlyListCorrelationReportSymbol { get; }
```

### IReadOnlyListCounterSnapshot

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CounterSnapshot\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<CounterSnapshot>> IReadOnlyListCounterSnapshot { get; }
```

### IReadOnlyListCustomAttributeInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<CustomAttributeInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<CustomAttributeInfo>> IReadOnlyListCustomAttributeInfo { get; }
```

### IReadOnlyListDgmlPathStep

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DgmlPathStep\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<DgmlPathStep>> IReadOnlyListDgmlPathStep { get; }
```

### IReadOnlyListDiffEntryAssemblyRefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<AssemblyRefInfo\>\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<DiffEntry<AssemblyRefInfo>>> IReadOnlyListDiffEntryAssemblyRefInfo { get; }
```

### IReadOnlyListDiffEntryMethodDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MethodDefInfo\>\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<DiffEntry<MethodDefInfo>>> IReadOnlyListDiffEntryMethodDefInfo { get; }
```

### IReadOnlyListDiffEntryTypeDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TypeDefInfo\>\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<DiffEntry<TypeDefInfo>>> IReadOnlyListDiffEntryTypeDefInfo { get; }
```

### IReadOnlyListFieldDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<FieldDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<FieldDefInfo>> IReadOnlyListFieldDefInfo { get; }
```

### IReadOnlyListGraphEdge

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<GraphEdge\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<GraphEdge>> IReadOnlyListGraphEdge { get; }
```

### IReadOnlyListGraphNode

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<GraphNode\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<GraphNode>> IReadOnlyListGraphNode { get; }
```

### IReadOnlyListIlInstruction

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<IlInstruction\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<IlInstruction>> IReadOnlyListIlInstruction { get; }
```

### IReadOnlyListLocalSlotInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<LocalSlotInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<LocalSlotInfo>> IReadOnlyListLocalSlotInfo { get; }
```

### IReadOnlyListMemberRefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MemberRefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<MemberRefInfo>> IReadOnlyListMemberRefInfo { get; }
```

### IReadOnlyListMethodDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MethodDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<MethodDefInfo>> IReadOnlyListMethodDefInfo { get; }
```

### IReadOnlyListMstatCandidatePayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatCandidatePayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<MstatCandidatePayload>> IReadOnlyListMstatCandidatePayload { get; }
```

### IReadOnlyListMstatContributorPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatContributorPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<MstatContributorPayload>> IReadOnlyListMstatContributorPayload { get; }
```

### IReadOnlyListMstatWhyChainPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatWhyChainPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<MstatWhyChainPayload>> IReadOnlyListMstatWhyChainPayload { get; }
```

### IReadOnlyListNativeAotSectionPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeAotSectionPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<NativeAotSectionPayload>> IReadOnlyListNativeAotSectionPayload { get; }
```

### IReadOnlyListNativeInstruction

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeInstruction\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<NativeInstruction>> IReadOnlyListNativeInstruction { get; }
```

### IReadOnlyListNativeOperand

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeOperand\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<NativeOperand>> IReadOnlyListNativeOperand { get; }
```

### IReadOnlyListNativeSourceLine

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSourceLine\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<NativeSourceLine>> IReadOnlyListNativeSourceLine { get; }
```

### IReadOnlyListNativeSymbol

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbol\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<NativeSymbol>> IReadOnlyListNativeSymbol { get; }
```

### IReadOnlyListNativeSymbolCandidatePayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolCandidatePayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<NativeSymbolCandidatePayload>> IReadOnlyListNativeSymbolCandidatePayload { get; }
```

### IReadOnlyListNuGetFileEntry

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NuGetFileEntry\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<NuGetFileEntry>> IReadOnlyListNuGetFileEntry { get; }
```

### IReadOnlyListOutputLine

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<OutputLine\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<OutputLine>> IReadOnlyListOutputLine { get; }
```

### IReadOnlyListRecoveredMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<RecoveredMethodPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<RecoveredMethodPayload>> IReadOnlyListRecoveredMethodPayload { get; }
```

### IReadOnlyListRecoveredType

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<RecoveredType\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<RecoveredType>> IReadOnlyListRecoveredType { get; }
```

### IReadOnlyListRecoveredTypePayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<RecoveredTypePayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<RecoveredTypePayload>> IReadOnlyListRecoveredTypePayload { get; }
```

### IReadOnlyListResourceInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<ResourceInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<ResourceInfo>> IReadOnlyListResourceInfo { get; }
```

### IReadOnlyListSectionInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SectionInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SectionInfo>> IReadOnlyListSectionInfo { get; }
```

### IReadOnlyListSequencePointInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SequencePointInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SequencePointInfo>> IReadOnlyListSequencePointInfo { get; }
```

### IReadOnlyListSizeBudgetEvaluation

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBudgetEvaluation\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SizeBudgetEvaluation>> IReadOnlyListSizeBudgetEvaluation { get; }
```

### IReadOnlyListSizeBudgetMetric

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBudgetMetric\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SizeBudgetMetric>> IReadOnlyListSizeBudgetMetric { get; }
```

### IReadOnlyListSizeBudgetViolation

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBudgetViolation\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SizeBudgetViolation>> IReadOnlyListSizeBudgetViolation { get; }
```

### IReadOnlyListSizeDiffAggregate

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeDiffAggregate\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SizeDiffAggregate>> IReadOnlyListSizeDiffAggregate { get; }
```

### IReadOnlyListSizeDiffContributor

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeDiffContributor\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SizeDiffContributor>> IReadOnlyListSizeDiffContributor { get; }
```

### IReadOnlyListSizeDiffKindCounts

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeDiffKindCounts\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SizeDiffKindCounts>> IReadOnlyListSizeDiffKindCounts { get; }
```

### IReadOnlyListSizeDiffNode

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeDiffNode\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SizeDiffNode>> IReadOnlyListSizeDiffNode { get; }
```

### IReadOnlyListSizeNode

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeNode\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SizeNode>> IReadOnlyListSizeNode { get; }
```

### IReadOnlyListSourceLinkMapping

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SourceLinkMapping\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<SourceLinkMapping>> IReadOnlyListSourceLinkMapping { get; }
```

### IReadOnlyListString

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<String\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<string>> IReadOnlyListString { get; }
```

### IReadOnlyListStringEntry

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<StringEntry\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<StringEntry>> IReadOnlyListStringEntry { get; }
```

### IReadOnlyListTraceEventEntry

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TraceEventEntry\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<TraceEventEntry>> IReadOnlyListTraceEventEntry { get; }
```

### IReadOnlyListTypeDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TypeDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<TypeDefInfo>> IReadOnlyListTypeDefInfo { get; }
```

### IReadOnlyListTypeRefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TypeRefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<TypeRefInfo>> IReadOnlyListTypeRefInfo { get; }
```

### IReadOnlyListWasmFunctionPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<WasmFunctionPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<WasmFunctionPayload>> IReadOnlyListWasmFunctionPayload { get; }
```

### IReadOnlyListWasmSectionPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<WasmSectionPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<IReadOnlyList<WasmSectionPayload>> IReadOnlyListWasmSectionPayload { get; }
```

### JsonElement

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<JsonElement> JsonElement { get; }
```

### ListBundleEntryPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<BundleEntryPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<List<BundleEntryPayload>> ListBundleEntryPayload { get; }
```

### ListDiscoveredSessionPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<DiscoveredSessionPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<List<DiscoveredSessionPayload>> ListDiscoveredSessionPayload { get; }
```

### ListIlLargestMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<IlLargestMethodPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<List<IlLargestMethodPayload>> ListIlLargestMethodPayload { get; }
```

### ListIlSearchResultPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<IlSearchResultPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<List<IlSearchResultPayload>> ListIlSearchResultPayload { get; }
```

### ListMethodDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MethodDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<List<MethodDefInfo>> ListMethodDefInfo { get; }
```

### ListMstatLargestMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatLargestMethodPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<List<MstatLargestMethodPayload>> ListMstatLargestMethodPayload { get; }
```

### ListNativeSymbolLargestMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolLargestMethodPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<List<NativeSymbolLargestMethodPayload>> ListNativeSymbolLargestMethodPayload { get; }
```

### ListRecoveredMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<RecoveredMethodPayload\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<List<RecoveredMethodPayload>> ListRecoveredMethodPayload { get; }
```

### LocalSlotInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<LocalSlotInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<LocalSlotInfo> LocalSlotInfo { get; }
```

### Machine

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Machine\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<Machine> Machine { get; }
```

### MemberRefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MemberRefInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MemberRefInfo> MemberRefInfo { get; }
```

### MemberRefKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MemberRefKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MemberRefKind> MemberRefKind { get; }
```

### MessagePayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MessagePayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MessagePayload> MessagePayload { get; }
```

### MetadataMemberSearchPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MetadataMemberSearchPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MetadataMemberSearchPayload> MetadataMemberSearchPayload { get; }
```

### MethodAttributes

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MethodAttributes\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MethodAttributes> MethodAttributes { get; }
```

### MethodDebugInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MethodDebugInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MethodDebugInfo> MethodDebugInfo { get; }
```

### MethodDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MethodDefInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MethodDefInfo> MethodDefInfo { get; }
```

### MethodImplAttributes

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MethodImplAttributes\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MethodImplAttributes> MethodImplAttributes { get; }
```

### MstatCandidatePayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatCandidatePayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MstatCandidatePayload> MstatCandidatePayload { get; }
```

### MstatContributorPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatContributorPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MstatContributorPayload> MstatContributorPayload { get; }
```

### MstatContributorsPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatContributorsPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MstatContributorsPayload> MstatContributorsPayload { get; }
```

### MstatFiltersPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatFiltersPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MstatFiltersPayload> MstatFiltersPayload { get; }
```

### MstatLargestMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatLargestMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MstatLargestMethodPayload> MstatLargestMethodPayload { get; }
```

### MstatMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MstatMethodPayload> MstatMethodPayload { get; }
```

### MstatSectionKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatSectionKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MstatSectionKind> MstatSectionKind { get; }
```

### MstatSourceSummaryPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatSourceSummaryPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MstatSourceSummaryPayload> MstatSourceSummaryPayload { get; }
```

### MstatWhyChainPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatWhyChainPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MstatWhyChainPayload> MstatWhyChainPayload { get; }
```

### MstatWhyPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<MstatWhyPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<MstatWhyPayload> MstatWhyPayload { get; }
```

### NativeAotInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeAotInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeAotInfo> NativeAotInfo { get; }
```

### NativeAotInfoPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeAotInfoPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeAotInfoPayload> NativeAotInfoPayload { get; }
```

### NativeAotSectionPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeAotSectionPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeAotSectionPayload> NativeAotSectionPayload { get; }
```

### NativeAotSectionsPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeAotSectionsPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeAotSectionsPayload> NativeAotSectionsPayload { get; }
```

### NativeArchitecture

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeArchitecture\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeArchitecture> NativeArchitecture { get; }
```

### NativeDisassemblyPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeDisassemblyPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeDisassemblyPayload> NativeDisassemblyPayload { get; }
```

### NativeFlowKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeFlowKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeFlowKind> NativeFlowKind { get; }
```

### NativeInstruction

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeInstruction> NativeInstruction { get; }
```

### NativeInstructionCategory

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeInstructionCategory\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeInstructionCategory> NativeInstructionCategory { get; }
```

### NativeLineLayout

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeLineLayout\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeLineLayout> NativeLineLayout { get; }
```

### NativeOperand

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeOperand\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeOperand> NativeOperand { get; }
```

### NativeOperandKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeOperandKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeOperandKind> NativeOperandKind { get; }
```

### NativeSourceLine

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSourceLine\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSourceLine> NativeSourceLine { get; }
```

### NativeSourceMap

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSourceMap\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSourceMap> NativeSourceMap { get; }
```

### NativeSymbol

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbol\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbol> NativeSymbol { get; }
```

### NativeSymbolAmbiguityPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolAmbiguityPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbolAmbiguityPayload> NativeSymbolAmbiguityPayload { get; }
```

### NativeSymbolCandidatePayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolCandidatePayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbolCandidatePayload> NativeSymbolCandidatePayload { get; }
```

### NativeSymbolInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbolInfo> NativeSymbolInfo { get; }
```

### NativeSymbolKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbolKind> NativeSymbolKind { get; }
```

### NativeSymbolLargestMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolLargestMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbolLargestMethodPayload> NativeSymbolLargestMethodPayload { get; }
```

### NativeSymbolMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbolMethodPayload> NativeSymbolMethodPayload { get; }
```

### NativeSymbolSource

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolSource\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbolSource> NativeSymbolSource { get; }
```

### NativeSymbolStatus

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolStatus\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbolStatus> NativeSymbolStatus { get; }
```

### NativeTargetKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeTargetKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeTargetKind> NativeTargetKind { get; }
```

### NuGetFileEntry

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NuGetFileEntry\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NuGetFileEntry> NuGetFileEntry { get; }
```

### NuGetPackagePayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NuGetPackagePayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NuGetPackagePayload> NuGetPackagePayload { get; }
```

### NullableBoolean

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Boolean\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<bool?> NullableBoolean { get; }
```

### NullableDouble

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Double\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<double?> NullableDouble { get; }
```

### NullableInt32

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Int32\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<int?> NullableInt32 { get; }
```

### NullableInt64

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Int64\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<long?> NullableInt64 { get; }
```

### NullableJsonElement

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<JsonElement\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<JsonElement?> NullableJsonElement { get; }
```

### NullableNativeLineLayout

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeLineLayout\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeLineLayout?> NullableNativeLineLayout { get; }
```

### NullableNativeSymbolSource

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolSource\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbolSource?> NullableNativeSymbolSource { get; }
```

### NullableNativeSymbolStatus

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<NativeSymbolStatus\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<NativeSymbolStatus?> NullableNativeSymbolStatus { get; }
```

### NullableUInt32

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<UInt32\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<uint?> NullableUInt32 { get; }
```

### NullableUInt64

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<UInt64\>\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ulong?> NullableUInt64 { get; }
```

### OperationStatusPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<OperationStatusPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<OperationStatusPayload> OperationStatusPayload { get; }
```

### OutputLine

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<OutputLine\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<OutputLine> OutputLine { get; }
```

### PdbProvenance

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<PdbProvenance\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<PdbProvenance> PdbProvenance { get; }
```

### PdbProvenanceKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<PdbProvenanceKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<PdbProvenanceKind> PdbProvenanceKind { get; }
```

### PeHeaders

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<PeHeaders\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<PeHeaders> PeHeaders { get; }
```

### PEMagic

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<PEMagic\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<PEMagic> PEMagic { get; }
```

### PreIlcSummary

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<PreIlcSummary\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<PreIlcSummary> PreIlcSummary { get; }
```

### Protocol

The protocol context configured with camel-case enum values.

**Returns:** [DotsiderJsonContext](/api/dotsider.core.protocol.dotsiderjsoncontext/)

```csharp
public static DotsiderJsonContext Protocol { get; }
```

### ReadyToRunAmbiguityPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<ReadyToRunAmbiguityPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ReadyToRunAmbiguityPayload> ReadyToRunAmbiguityPayload { get; }
```

### ReadyToRunMethodReport

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<ReadyToRunMethodReport\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ReadyToRunMethodReport> ReadyToRunMethodReport { get; }
```

### ReadyToRunNativeAvailability

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<ReadyToRunNativeAvailability\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ReadyToRunNativeAvailability> ReadyToRunNativeAvailability { get; }
```

### ReadyToRunSummary

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<ReadyToRunSummary\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ReadyToRunSummary> ReadyToRunSummary { get; }
```

### RecoveredMemberSearchPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<RecoveredMemberSearchPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<RecoveredMemberSearchPayload> RecoveredMemberSearchPayload { get; }
```

### RecoveredMethodPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<RecoveredMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<RecoveredMethodPayload> RecoveredMethodPayload { get; }
```

### RecoveredType

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<RecoveredType\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<RecoveredType> RecoveredType { get; }
```

### RecoveredTypePayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<RecoveredTypePayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<RecoveredTypePayload> RecoveredTypePayload { get; }
```

### ResolvedAssemblyInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<ResolvedAssemblyInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ResolvedAssemblyInfo> ResolvedAssemblyInfo { get; }
```

### ResourceInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<ResourceInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ResourceInfo> ResourceInfo { get; }
```

### SectionCharacteristics

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SectionCharacteristics\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SectionCharacteristics> SectionCharacteristics { get; }
```

### SectionInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SectionInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SectionInfo> SectionInfo { get; }
```

### SequencePointInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SequencePointInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SequencePointInfo> SequencePointInfo { get; }
```

### SessionInfoPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SessionInfoPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SessionInfoPayload> SessionInfoPayload { get; }
```

### SizeBasis

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBasis\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeBasis> SizeBasis { get; }
```

### SizeBudget

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBudget\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeBudget> SizeBudget { get; }
```

### SizeBudgetEvaluation

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBudgetEvaluation\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeBudgetEvaluation> SizeBudgetEvaluation { get; }
```

### SizeBudgetMetric

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBudgetMetric\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeBudgetMetric> SizeBudgetMetric { get; }
```

### SizeBudgetPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBudgetPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeBudgetPayload> SizeBudgetPayload { get; }
```

### SizeBudgetScope

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBudgetScope\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeBudgetScope> SizeBudgetScope { get; }
```

### SizeBudgetSeverity

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBudgetSeverity\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeBudgetSeverity> SizeBudgetSeverity { get; }
```

### SizeBudgetViolation

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeBudgetViolation\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeBudgetViolation> SizeBudgetViolation { get; }
```

### SizeDiffAggregate

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeDiffAggregate> SizeDiffAggregate { get; }
```

### SizeDiffContributor

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeDiffContributor\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeDiffContributor> SizeDiffContributor { get; }
```

### SizeDiffKindCounts

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeDiffKindCounts\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeDiffKindCounts> SizeDiffKindCounts { get; }
```

### SizeDiffNode

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeDiffNode\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeDiffNode> SizeDiffNode { get; }
```

### SizeDiffPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeDiffPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeDiffPayload> SizeDiffPayload { get; }
```

### SizeDiffSummary

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeDiffSummary\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeDiffSummary> SizeDiffSummary { get; }
```

### SizeNode

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeNode> SizeNode { get; }
```

### SizeNodeKind

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SizeNodeKind\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SizeNodeKind> SizeNodeKind { get; }
```

### SourceLinkInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SourceLinkInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SourceLinkInfo> SourceLinkInfo { get; }
```

### SourceLinkMapping

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<SourceLinkMapping\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<SourceLinkMapping> SourceLinkMapping { get; }
```

### String

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<String\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<string> String { get; }
```

### StringArray

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<String[]\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<string[]> StringArray { get; }
```

### StringEntry

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<StringEntry> StringEntry { get; }
```

### StringSource

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<StringSource\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<StringSource> StringSource { get; }
```

### StringsPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<StringsPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<StringsPayload> StringsPayload { get; }
```

### Subsystem

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<Subsystem\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<Subsystem> Subsystem { get; }
```

### TimeSpan

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TimeSpan\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<TimeSpan> TimeSpan { get; }
```

### TokenResolutionPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TokenResolutionPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<TokenResolutionPayload> TokenResolutionPayload { get; }
```

### TraceEventCategory

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TraceEventCategory\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<TraceEventCategory> TraceEventCategory { get; }
```

### TraceEventEntry

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TraceEventEntry\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<TraceEventEntry> TraceEventEntry { get; }
```

### TraceSummary

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TraceSummary\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<TraceSummary> TraceSummary { get; }
```

### TypeAttributes

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TypeAttributes\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<TypeAttributes> TypeAttributes { get; }
```

### TypeDefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TypeDefInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<TypeDefInfo> TypeDefInfo { get; }
```

### TypeRefInfo

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<TypeRefInfo\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<TypeRefInfo> TypeRefInfo { get; }
```

### UInt16

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<UInt16\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ushort> UInt16 { get; }
```

### UInt32

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<UInt32\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<uint> UInt32 { get; }
```

### UInt64

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<ulong> UInt64 { get; }
```

### WasmFunctionPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<WasmFunctionPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<WasmFunctionPayload> WasmFunctionPayload { get; }
```

### WasmFunctionsPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<WasmFunctionsPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<WasmFunctionsPayload> WasmFunctionsPayload { get; }
```

### WasmSectionPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<WasmSectionPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<WasmSectionPayload> WasmSectionPayload { get; }
```

### WasmSectionsPayload

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<WasmSectionsPayload\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<WasmSectionsPayload> WasmSectionsPayload { get; }
```

### WasmSummary

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<WasmSummary\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<WasmSummary> WasmSummary { get; }
```

### WebcilSummary

Defines the source generated JSON serialization contract metadata for a given type.

**Returns:** [JsonTypeInfo\<WebcilSummary\>](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo-1)

```csharp
public JsonTypeInfo<WebcilSummary> WebcilSummary { get; }
```

## Methods

### CreateOptions()

Creates the JSON options shared by all dotsider source-generated contexts.

**Returns:** [JsonSerializerOptions](https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializeroptions)

```csharp
public static JsonSerializerOptions CreateOptions()
```

### GetTypeInfo(Type)

**Parameters:**

- `type` ([Type](https://learn.microsoft.com/dotnet/api/system.type))

**Returns:** [JsonTypeInfo](https://learn.microsoft.com/dotnet/api/system.text.json.serialization.metadata.jsontypeinfo)

```csharp
public override JsonTypeInfo? GetTypeInfo(Type type)
```
