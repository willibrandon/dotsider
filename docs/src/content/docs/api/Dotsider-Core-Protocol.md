---
title: "Dotsider.Core.Protocol"
slug: api/dotsider.core.protocol
sidebar:
  order: 4
  attrs:
    data-api-namespace: "true"
---

## Classes

### [AssemblyInfoPayload](/api/dotsider.core.protocol.assemblyinfopayload/)

Assembly identity and analysis capabilities exposed by protocol surfaces.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record AssemblyInfoPayload : IEquatable<AssemblyInfoPayload>
```

### [AssemblyInfoPayloadBuilder](/api/dotsider.core.protocol.assemblyinfopayloadbuilder/)

Builds the shared assembly-information contract used by the CLI and MCP server.
Centralizes the mapping from an analyzer into the public protocol shape.
Keeps command-line and MCP responses consistent without reflection.

```csharp
public static class AssemblyInfoPayloadBuilder
```

### [BundleEntryPayload](/api/dotsider.core.protocol.bundleentrypayload/)

One single-file bundle manifest entry.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record BundleEntryPayload : IEquatable<BundleEntryPayload>
```

### [BundleInfoPayload](/api/dotsider.core.protocol.bundleinfopayload/)

Single-file bundle identity and total content size.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record BundleInfoPayload : IEquatable<BundleInfoPayload>
```

### [BundleProbePayload](/api/dotsider.core.protocol.bundleprobepayload/)

A single-file bundle probe result.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record BundleProbePayload : IEquatable<BundleProbePayload>
```

### [ByteRangePayload](/api/dotsider.core.protocol.byterangepayload/)

Bytes read from a binary at a requested offset.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record ByteRangePayload : IEquatable<ByteRangePayload>
```

### [CurrentViewPayload](/api/dotsider.core.protocol.currentviewpayload/)

The current interactive view of a standard dotsider session.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record CurrentViewPayload : IEquatable<CurrentViewPayload>
```

### [DependencyGraphPayload](/api/dotsider.core.protocol.dependencygraphpayload/)

A dependency graph suitable for protocol and MCP responses.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record DependencyGraphPayload : IEquatable<DependencyGraphPayload>
```

### [DiscoveredSessionPayload](/api/dotsider.core.protocol.discoveredsessionpayload/)

A live dotsider session discovered over its diagnostics socket.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record DiscoveredSessionPayload : IEquatable<DiscoveredSessionPayload>
```

### [DotsiderJsonContext](/api/dotsider.core.protocol.dotsiderjsoncontext/)

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

### [DotsiderProtocol](/api/dotsider.core.protocol.dotsiderprotocol/)

Constants for the dotsider diagnostics protocol.

```csharp
public static class DotsiderProtocol
```

### [DotsiderRequest](/api/dotsider.core.protocol.dotsiderrequest/)

JSON request sent to a dotsider diagnostics socket.

```csharp
public sealed class DotsiderRequest
```

### [DotsiderResponse](/api/dotsider.core.protocol.dotsiderresponse/)

JSON response from a dotsider diagnostics socket.

```csharp
public sealed class DotsiderResponse
```

### [FrameworkAssemblyInfo](/api/dotsider.core.protocol.frameworkassemblyinfo/)

Result of resolving an assembly from the system .NET shared framework.
Includes the full path and the runtime pack that provided it.

```csharp
public sealed record FrameworkAssemblyInfo : IEquatable<FrameworkAssemblyInfo>
```

### [IlDisassemblyPayload](/api/dotsider.core.protocol.ildisassemblypayload/)

IL and optional portable-PDB data for one method.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record IlDisassemblyPayload : IEquatable<IlDisassemblyPayload>
```

### [IlLargestMethodPayload](/api/dotsider.core.protocol.illargestmethodpayload/)

A large IL method.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record IlLargestMethodPayload : IEquatable<IlLargestMethodPayload>
```

### [IlSearchResultPayload](/api/dotsider.core.protocol.ilsearchresultpayload/)

Opcode matches within one method.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record IlSearchResultPayload : IEquatable<IlSearchResultPayload>
```

### [MessagePayload](/api/dotsider.core.protocol.messagepayload/)

A status or queued-operation message.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MessagePayload : IEquatable<MessagePayload>
```

### [MetadataMemberSearchPayload](/api/dotsider.core.protocol.metadatamembersearchpayload/)

Metadata-backed member-search results.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MetadataMemberSearchPayload : IEquatable<MetadataMemberSearchPayload>
```

### [MstatCandidatePayload](/api/dotsider.core.protocol.mstatcandidatepayload/)

A possible match for an ambiguous mstat query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatCandidatePayload : IEquatable<MstatCandidatePayload>
```

### [MstatContributorPayload](/api/dotsider.core.protocol.mstatcontributorpayload/)

One Native AOT size contributor.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatContributorPayload : IEquatable<MstatContributorPayload>
```

### [MstatContributorsPayload](/api/dotsider.core.protocol.mstatcontributorspayload/)

Native AOT size-contributor query results.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatContributorsPayload : IEquatable<MstatContributorsPayload>
```

### [MstatFiltersPayload](/api/dotsider.core.protocol.mstatfilterspayload/)

Filters applied to an mstat contributor query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatFiltersPayload : IEquatable<MstatFiltersPayload>
```

### [MstatLargestMethodPayload](/api/dotsider.core.protocol.mstatlargestmethodpayload/)

A large method reported by mstat.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatLargestMethodPayload : IEquatable<MstatLargestMethodPayload>
```

### [MstatMethodPayload](/api/dotsider.core.protocol.mstatmethodpayload/)

A method identity extracted from an mstat entry.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatMethodPayload : IEquatable<MstatMethodPayload>
```

### [MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/)

Summary of an mstat source and its matching binary.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatSourceSummaryPayload : IEquatable<MstatSourceSummaryPayload>
```

### [MstatWhyChainPayload](/api/dotsider.core.protocol.mstatwhychainpayload/)

One dependency chain explaining a Native AOT size contributor.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatWhyChainPayload : IEquatable<MstatWhyChainPayload>
```

### [MstatWhyPayload](/api/dotsider.core.protocol.mstatwhypayload/)

Outcome of a Native AOT dependency explanation query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatWhyPayload : IEquatable<MstatWhyPayload>
```

### [NativeAotInfoPayload](/api/dotsider.core.protocol.nativeaotinfopayload/)

Native AOT identity and sidecar facts.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeAotInfoPayload : IEquatable<NativeAotInfoPayload>
```

### [NativeAotPayloadBuilder](/api/dotsider.core.protocol.nativeaotpayloadbuilder/)

Builds JSON-ready Native AOT payloads shared by direct MCP tools and the diagnostics session
protocol, so the two transports return the same facts and error semantics.

```csharp
public static class NativeAotPayloadBuilder
```

### [NativeAotSectionPayload](/api/dotsider.core.protocol.nativeaotsectionpayload/)

A Native AOT module-section row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeAotSectionPayload : IEquatable<NativeAotSectionPayload>
```

### [NativeAotSectionsPayload](/api/dotsider.core.protocol.nativeaotsectionspayload/)

A Native AOT module-section inventory.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeAotSectionsPayload : IEquatable<NativeAotSectionsPayload>
```

### [NativeDisassemblyPayload](/api/dotsider.core.protocol.nativedisassemblypayload/)

Decoded native instructions for one symbol.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeDisassemblyPayload : IEquatable<NativeDisassemblyPayload>
```

### [NativeSymbolAmbiguityPayload](/api/dotsider.core.protocol.nativesymbolambiguitypayload/)

An ambiguous native-symbol query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeSymbolAmbiguityPayload : IEquatable<NativeSymbolAmbiguityPayload>
```

### [NativeSymbolCandidatePayload](/api/dotsider.core.protocol.nativesymbolcandidatepayload/)

One candidate for an ambiguous native-symbol query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeSymbolCandidatePayload : IEquatable<NativeSymbolCandidatePayload>
```

### [NativeSymbolLargestMethodPayload](/api/dotsider.core.protocol.nativesymbollargestmethodpayload/)

A large method reported by native symbols.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeSymbolLargestMethodPayload : IEquatable<NativeSymbolLargestMethodPayload>
```

### [NativeSymbolMethodPayload](/api/dotsider.core.protocol.nativesymbolmethodpayload/)

A native-symbol method identity.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeSymbolMethodPayload : IEquatable<NativeSymbolMethodPayload>
```

### [NuGetPackagePayload](/api/dotsider.core.protocol.nugetpackagepayload/)

NuGet package identity and managed payload files.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NuGetPackagePayload : IEquatable<NuGetPackagePayload>
```

### [OperationStatusPayload](/api/dotsider.core.protocol.operationstatuspayload/)

A queued-operation status.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record OperationStatusPayload : IEquatable<OperationStatusPayload>
```

### [PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/)

Compact provenance for the managed inputs used to produce a Native AOT binary.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record PreIlcSummary : IEquatable<PreIlcSummary>
```

### [ReadyToRunAmbiguityPayload](/api/dotsider.core.protocol.readytorunambiguitypayload/)

An ambiguous ReadyToRun method query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record ReadyToRunAmbiguityPayload : IEquatable<ReadyToRunAmbiguityPayload>
```

### [ReadyToRunSummary](/api/dotsider.core.protocol.readytorunsummary/)

Compact ReadyToRun image facts returned by assembly inspection.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record ReadyToRunSummary : IEquatable<ReadyToRunSummary>
```

### [RecoveredMemberSearchPayload](/api/dotsider.core.protocol.recoveredmembersearchpayload/)

Recovered Native AOT member-search results.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record RecoveredMemberSearchPayload : IEquatable<RecoveredMemberSearchPayload>
```

### [RecoveredMethodPayload](/api/dotsider.core.protocol.recoveredmethodpayload/)

A recovered Native AOT method row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record RecoveredMethodPayload : IEquatable<RecoveredMethodPayload>
```

### [RecoveredTypePayload](/api/dotsider.core.protocol.recoveredtypepayload/)

A recovered Native AOT type row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record RecoveredTypePayload : IEquatable<RecoveredTypePayload>
```

### [ResolvedAssemblyInfo](/api/dotsider.core.protocol.resolvedassemblyinfo/)

Serialization-safe representation of an assembly resolution result.
Used in protocol and MCP responses where [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)
cannot be serialized directly because bundle and module results contain raw bytes.

```csharp
public sealed record ResolvedAssemblyInfo : IEquatable<ResolvedAssemblyInfo>
```

### [SessionInfoPayload](/api/dotsider.core.protocol.sessioninfopayload/)

Assembly and view state returned for one live session.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record SessionInfoPayload : IEquatable<SessionInfoPayload>
```

### [SizeBudgetPayload](/api/dotsider.core.protocol.sizebudgetpayload/)

Size-budget results for one mstat-backed input.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record SizeBudgetPayload : IEquatable<SizeBudgetPayload>
```

### [SizeDiffPayload](/api/dotsider.core.protocol.sizediffpayload/)

Size differences between two mstat-backed inputs.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record SizeDiffPayload : IEquatable<SizeDiffPayload>
```

### [SizeDiffPayloadBuilder](/api/dotsider.core.protocol.sizediffpayloadbuilder/)

Builds the serializable payloads the `diff-size` and `check-size-budgets`
surfaces return. The MCP server's direct mode and the running-session protocol handler
both call these, so the two transports cannot drift apart in shape or semantics.

```csharp
public static class SizeDiffPayloadBuilder
```

### [StringsPayload](/api/dotsider.core.protocol.stringspayload/)

All string categories extracted from a binary.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record StringsPayload : IEquatable<StringsPayload>
```

### [TokenResolutionPayload](/api/dotsider.core.protocol.tokenresolutionpayload/)

A metadata token resolution.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record TokenResolutionPayload : IEquatable<TokenResolutionPayload>
```

### [WasmFunctionPayload](/api/dotsider.core.protocol.wasmfunctionpayload/)

A WebAssembly function row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WasmFunctionPayload : IEquatable<WasmFunctionPayload>
```

### [WasmFunctionsPayload](/api/dotsider.core.protocol.wasmfunctionspayload/)

A WebAssembly function inventory.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WasmFunctionsPayload : IEquatable<WasmFunctionsPayload>
```

### [WasmPayloadBuilder](/api/dotsider.core.protocol.wasmpayloadbuilder/)

Builds JSON-ready WebAssembly payloads shared by direct MCP tools, the CLI, and the
diagnostics session protocol. The payloads describe raw SDK-produced Wasm modules such as
`dotnet.native.wasm`, not ECMA-335 metadata assemblies.

```csharp
public static class WasmPayloadBuilder
```

### [WasmSectionPayload](/api/dotsider.core.protocol.wasmsectionpayload/)

A WebAssembly section row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WasmSectionPayload : IEquatable<WasmSectionPayload>
```

### [WasmSectionsPayload](/api/dotsider.core.protocol.wasmsectionspayload/)

A WebAssembly section inventory.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WasmSectionsPayload : IEquatable<WasmSectionsPayload>
```

### [WasmSummary](/api/dotsider.core.protocol.wasmsummary/)

Compact WebAssembly module facts.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WasmSummary : IEquatable<WasmSummary>
```

### [WebcilPayloadBuilder](/api/dotsider.core.protocol.webcilpayloadbuilder/)

Builds JSON-ready Webcil payloads shared by CLI, MCP, and diagnostics session output.
Webcil is a managed assembly container used in browser-wasm publishes, so the payload is
provenance beside the normal metadata/IL facts rather than a separate native module view.

```csharp
public static class WebcilPayloadBuilder
```

### [WebcilSummary](/api/dotsider.core.protocol.webcilsummary/)

Compact Webcil container facts.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WebcilSummary : IEquatable<WebcilSummary>
```
