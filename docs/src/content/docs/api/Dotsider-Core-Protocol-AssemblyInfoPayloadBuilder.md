---
title: "AssemblyInfoPayloadBuilder"
description: "Builds the shared assembly-information contract used by the CLI and MCP server. Centralizes the mapping from an analyzer into the public protocol shape. Keeps command-line and MCP responses consistent without reflection."
slug: api/dotsider.core.protocol.assemblyinfopayloadbuilder
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Builds the shared assembly-information contract used by the CLI and MCP server.
Centralizes the mapping from an analyzer into the public protocol shape.
Keeps command-line and MCP responses consistent without reflection.

```csharp
public static class AssemblyInfoPayloadBuilder
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyInfoPayloadBuilder**

## Methods

### Build(AssemblyAnalyzer, string?)

Builds assembly identity, format, symbol, and sidecar facts.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/))
- `mode` ([String](https://learn.microsoft.com/dotnet/api/system.string))

**Returns:** [AssemblyInfoPayload](/api/dotsider.core.protocol.assemblyinfopayload/)

```csharp
public static AssemblyInfoPayload Build(AssemblyAnalyzer analyzer, string? mode = null)
```
