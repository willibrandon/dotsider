---
title: "NativeAotPayloadBuilder"
description: "Builds JSON-ready Native AOT payloads shared by direct MCP tools and the diagnostics session protocol, so the two transports return the same facts and error semantics."
slug: api/dotsider.core.protocol.nativeaotpayloadbuilder
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Builds JSON-ready Native AOT payloads shared by direct MCP tools and the diagnostics session
protocol, so the two transports return the same facts and error semantics.

```csharp
public static class NativeAotPayloadBuilder
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeAotPayloadBuilder**

## Methods

### BuildInfo(AssemblyAnalyzer)

Builds a Native AOT identity and sidecar summary for an analyzer.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/))

**Returns:** [NativeAotInfoPayload](/api/dotsider.core.protocol.nativeaotinfopayload/)

```csharp
public static NativeAotInfoPayload BuildInfo(AssemblyAnalyzer analyzer)
```

### BuildLargestMethods(AssemblyAnalyzer, int?)

Builds largest-method rows, using native mstat data for Native AOT.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/))
- `maxResults` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

**Returns:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

```csharp
public static JsonElement BuildLargestMethods(AssemblyAnalyzer analyzer, int? maxResults)
```

### BuildMemberSearch(AssemblyAnalyzer, string, int?, bool)

Builds member-search results, falling back to recovered Native AOT metadata.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/))
- `query` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `maxResults` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `includeCompilerGenerated` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))

**Returns:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

```csharp
public static JsonElement BuildMemberSearch(AssemblyAnalyzer analyzer, string query, int? maxResults, bool includeCompilerGenerated)
```

### BuildMethodInventory(AssemblyAnalyzer, string?, string?, int?)

Builds method-inventory rows, falling back to recovered Native AOT methods.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/))
- `typeName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `query` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `maxResults` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

**Returns:** [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)

```csharp
public static JsonElement BuildMethodInventory(AssemblyAnalyzer analyzer, string? typeName, string? query, int? maxResults)
```

### BuildSections(AssemblyAnalyzer)

Builds the Native AOT ReadyToRun module-section table payload.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/))

**Returns:** [NativeAotSectionsPayload](/api/dotsider.core.protocol.nativeaotsectionspayload/)

```csharp
public static NativeAotSectionsPayload BuildSections(AssemblyAnalyzer analyzer)
```

### BuildSizeContributors(MstatSource, string?, string?, string?, string?, int?, bool, int?)

Builds top Native AOT size contributors from an mstat-backed input.

**Parameters:**

- `source` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/))
- `query` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `section` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `namespaceName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `topN` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `includeWhy` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `maxWhyChains` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

**Returns:** [MstatContributorsPayload](/api/dotsider.core.protocol.mstatcontributorspayload/)

```csharp
public static MstatContributorsPayload BuildSizeContributors(MstatSource source, string? query, string? section, string? assemblyName, string? namespaceName, int? topN, bool includeWhy, int? maxWhyChains)
```

### BuildWhy(MstatSource, string, int?, int?)

Builds a Native AOT DGML explanation for one mstat contributor target.

**Parameters:**

- `source` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/))
- `target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `maxCandidates` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `maxWhyChains` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

**Returns:** [MstatWhyPayload](/api/dotsider.core.protocol.mstatwhypayload/)

```csharp
public static MstatWhyPayload BuildWhy(MstatSource source, string target, int? maxCandidates, int? maxWhyChains)
```

### ResolveMstatSource(AssemblyAnalyzer)

Resolves a Native AOT analyzer's mstat source, or null when no size report exists.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/))

**Returns:** [MstatSource](/api/dotsider.core.analysis.models.mstatsource/)

```csharp
public static MstatSource? ResolveMstatSource(AssemblyAnalyzer analyzer)
```

## Fields

### DefaultMaxCandidates

The default candidate count returned for ambiguous Native AOT why queries.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public const int DefaultMaxCandidates = 20
```

### DefaultMaxWhyChains

The default number of DGML chains shown for an aggregate mstat entry.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public const int DefaultMaxWhyChains = 3
```

### DefaultTopN

The default number of size contributors returned by Native AOT size tools.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public const int DefaultTopN = 20
```
