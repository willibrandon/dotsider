---
title: "AssemblyOpenResult.ApphostWithCompanion"
description: "The file is a native apphost with a companion managed .dll on disk. The caller decides when to redirect (e.g. showing a dialog first)."
slug: api/dotsider.core.analysis.models.assemblyopenresult.apphostwithcompanion
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The file is a native apphost with a companion managed .dll on disk.
The caller decides when to redirect (e.g. showing a dialog first).

```csharp
public sealed record AssemblyOpenResult.ApphostWithCompanion : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.ApphostWithCompanion>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) → **AssemblyOpenResult.ApphostWithCompanion**

## Implements

- [IEquatable\<AssemblyOpenResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<ApphostWithCompanion\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ApphostWithCompanion(AssemblyAnalyzer, string)

The file is a native apphost with a companion managed .dll on disk.
The caller decides when to redirect (e.g. showing a dialog first).

**Parameters:**

- `HostAnalyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer for the native apphost (no metadata).
- `CompanionDllPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full path to the companion managed .dll.

```csharp
public ApphostWithCompanion(AssemblyAnalyzer HostAnalyzer, string CompanionDllPath)
```

## Properties

### CompanionDllPath

Full path to the companion managed .dll.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string CompanionDllPath { get; init; }
```

### HostAnalyzer

The analyzer for the native apphost (no metadata).

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer HostAnalyzer { get; init; }
```

