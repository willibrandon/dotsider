---
title: "WasmPayloadBuilder"
description: "Builds JSON-ready WebAssembly payloads shared by direct MCP tools, the CLI, and the diagnostics session protocol. The payloads describe raw SDK-produced Wasm modules such as dotnet.native.wasm, not ECMA-335 metadata assemblies."
slug: api/dotsider.core.protocol.wasmpayloadbuilder
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Builds JSON-ready WebAssembly payloads shared by direct MCP tools, the CLI, and the
diagnostics session protocol. The payloads describe raw SDK-produced Wasm modules such as
`dotnet.native.wasm`, not ECMA-335 metadata assemblies.

```csharp
public static class WasmPayloadBuilder
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmPayloadBuilder**

## Methods

### BuildFunctions(AssemblyAnalyzer)

Builds a function inventory for a WebAssembly module. Imported functions and file-backed
defined functions share the same Wasm function-index space, matching direct call operands
and symbol-map entries.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer that opened a raw Wasm module.

**Returns:** [WasmFunctionsPayload](/api/dotsider.core.protocol.wasmfunctionspayload/)

A JSON-ready function inventory payload.

```csharp
public static WasmFunctionsPayload BuildFunctions(AssemblyAnalyzer analyzer)
```

### BuildSections(AssemblyAnalyzer)

Builds a section-table payload for a WebAssembly module. Each section keeps its raw id,
display name, file payload offset, and payload size so callers can jump to the bytes that
the SDK emitted.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer that opened a raw Wasm module.

**Returns:** [WasmSectionsPayload](/api/dotsider.core.protocol.wasmsectionspayload/)

A JSON-ready section table payload.

```csharp
public static WasmSectionsPayload BuildSections(AssemblyAnalyzer analyzer)
```

### BuildSummary(AssemblyAnalyzer)

Builds a compact WebAssembly module summary for protocol surfaces. Returns null when the
analyzer is not a raw Wasm module so callers can include it unconditionally beside other
binary-kind summaries.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer whose raw Wasm summary should be serialized.

**Returns:** [WasmSummary](/api/dotsider.core.protocol.wasmsummary/)

A JSON-ready summary object, or null when the analyzer is not raw Wasm.

```csharp
public static WasmSummary? BuildSummary(AssemblyAnalyzer analyzer)
```
