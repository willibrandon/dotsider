---
title: "AssemblyOpenResult.NativeAot"
description: "The file is a Native AOT compiled .NET binary: a valid PE, ELF, or Mach-O with no COR header whose image embeds a validated ReadyToRun header. No metadata is available, but PE structure, native import/export/load-config directories, and raw strings are."
slug: api/dotsider.core.analysis.models.assemblyopenresult.nativeaot
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The file is a Native AOT compiled .NET binary: a valid PE, ELF, or Mach-O
with no COR header whose image embeds a validated ReadyToRun header. No
metadata is available, but PE structure, native import/export/load-config
directories, and raw strings are.

```csharp
public sealed record AssemblyOpenResult.NativeAot : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.NativeAot>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) → **AssemblyOpenResult.NativeAot**

## Implements

- [IEquatable\<AssemblyOpenResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<NativeAot\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeAot(AssemblyAnalyzer)

The file is a Native AOT compiled .NET binary: a valid PE, ELF, or Mach-O
with no COR header whose image embeds a validated ReadyToRun header. No
metadata is available, but PE structure, native import/export/load-config
directories, and raw strings are.

**Parameters:**

- `Analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer for the Native AOT binary (no metadata).

```csharp
public NativeAot(AssemblyAnalyzer Analyzer)
```

## Properties

### Analyzer

The analyzer for the Native AOT binary (no metadata).

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer Analyzer { get; init; }
```

