---
title: "AssemblyOpenResult.Direct"
description: "Direct load — the file is a .dll or .exe with metadata, or a native binary with no metadata (NativeAOT, unknown format)."
slug: api/dotsider.core.analysis.models.assemblyopenresult.direct
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Direct load — the file is a .dll or .exe with metadata, or a native binary
with no metadata (NativeAOT, unknown format).

```csharp
public sealed record AssemblyOpenResult.Direct : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.Direct>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) → **AssemblyOpenResult.Direct**

## Implements

- [IEquatable\<AssemblyOpenResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<Direct\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### Direct(AssemblyAnalyzer)

Direct load — the file is a .dll or .exe with metadata, or a native binary
with no metadata (NativeAOT, unknown format).

**Parameters:**

- `Analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer for the opened file.

```csharp
public Direct(AssemblyAnalyzer Analyzer)
```

## Properties

### Analyzer

The analyzer for the opened file.

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer Analyzer { get; init; }
```

