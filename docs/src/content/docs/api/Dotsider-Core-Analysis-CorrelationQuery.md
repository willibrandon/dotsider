---
title: "CorrelationQuery"
description: "Resolves a \"method or address\" query against an AOT binary's pre-ILC companion set and correlation index, producing the one CorrelationReport the CLI, session, and MCP surfaces all render. Attaches the companions on demand and builds the index once; ambiguity is surfaced as candidates, never resolved by picking the first match."
slug: api/dotsider.core.analysis.correlationquery
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Resolves a "method or address" query against an AOT binary's pre-ILC companion set and
correlation index, producing the one [CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/) the CLI, session, and
MCP surfaces all render. Attaches the companions on demand and builds the index once;
ambiguity is surfaced as candidates, never resolved by picking the first match.

```csharp
public static class CorrelationQuery
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **CorrelationQuery**

## Methods

### Resolve(AssemblyAnalyzer, string, CancellationToken)

Resolves methodOrAddress against analyzer: a
`0x`-prefixed value is looked up by native address; anything else is matched by
method name (optionally `Type.Method` / `Type::Method`) across the whole
companion set. Attaches the pre-ILC companions if they are not yet attached.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The AOT binary's analyzer.
- `methodOrAddress` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A method name, a qualified `Type.Method`, or a `0x` native address.
- `cancellationToken` ([CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)): Cancels the disassembly and match sweep.

**Returns:** [CorrelationQueryResult](/api/dotsider.core.analysis.models.correlationqueryresult/)

The resolved report, the ambiguous candidates, or the reason nothing resolved.

```csharp
public static CorrelationQueryResult Resolve(AssemblyAnalyzer analyzer, string methodOrAddress, CancellationToken cancellationToken)
```
