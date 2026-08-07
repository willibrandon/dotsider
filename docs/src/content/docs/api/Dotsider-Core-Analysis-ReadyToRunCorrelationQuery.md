---
title: "ReadyToRunCorrelationQuery"
description: "Resolves a \"method or address\" query against a ReadyToRun image and builds the one ReadyToRunMethodReport the CLI, MCP, and session surfaces all render. A method name, a 0x06… token, or a 0x… native address all resolve here; a value that is both a valid token and a covered address is reported ambiguous rather than guessed. Methods present in metadata but not precompiled resolve as IL-only rather than \"not found\"."
slug: api/dotsider.core.analysis.readytoruncorrelationquery
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Resolves a "method or address" query against a ReadyToRun image and builds the one
[ReadyToRunMethodReport](/api/dotsider.core.analysis.models.readytorunmethodreport/) the CLI, MCP, and session surfaces all render. A method
name, a `0x06…` token, or a `0x…` native address all resolve here; a value that is
both a valid token and a covered address is reported ambiguous rather than guessed. Methods
present in metadata but not precompiled resolve as IL-only rather than "not found".

```csharp
public static class ReadyToRunCorrelationQuery
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunCorrelationQuery**

## Methods

### Resolve(AssemblyAnalyzer, string, CancellationToken)

Resolves methodOrAddress against analyzer.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The ReadyToRun image's analyzer.
- `methodOrAddress` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A method name, a qualified `Type.Method`, a `0x06…` token, or a `0x…` native address.
- `cancellationToken` ([CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)): Cancels the disassembly and match sweep.

**Returns:** [ReadyToRunQueryResult](/api/dotsider.core.analysis.models.readytorunqueryresult/)

```csharp
public static ReadyToRunQueryResult Resolve(AssemblyAnalyzer analyzer, string methodOrAddress, CancellationToken cancellationToken)
```
