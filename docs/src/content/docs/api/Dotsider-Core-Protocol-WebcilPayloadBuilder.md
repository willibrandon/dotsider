---
title: "WebcilPayloadBuilder"
description: "Builds JSON-ready Webcil payloads shared by CLI, MCP, and diagnostics session output. Webcil is a managed assembly container used in browser-wasm publishes, so the payload is provenance beside the normal metadata/IL facts rather than a separate native module view."
slug: api/dotsider.core.protocol.webcilpayloadbuilder
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Builds JSON-ready Webcil payloads shared by CLI, MCP, and diagnostics session output.
Webcil is a managed assembly container used in browser-wasm publishes, so the payload is
provenance beside the normal metadata/IL facts rather than a separate native module view.

```csharp
public static class WebcilPayloadBuilder
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WebcilPayloadBuilder**

## Methods

### BuildSummary(AssemblyAnalyzer)

Builds a compact Webcil summary for protocol surfaces. Returns null when the analyzer did
not open a Webcil assembly, allowing callers to include the property unconditionally.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer whose Webcil provenance should be serialized.

**Returns:** [Object](https://learn.microsoft.com/dotnet/api/system.object)

A JSON-ready Webcil summary object, or null when the analyzer is not Webcil.

```csharp
public static object? BuildSummary(AssemblyAnalyzer analyzer)
```

