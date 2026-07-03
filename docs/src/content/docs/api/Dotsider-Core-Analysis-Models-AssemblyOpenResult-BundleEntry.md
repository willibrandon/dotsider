---
title: "AssemblyOpenResult.BundleEntry"
description: "The file is a single-file bundle. The entry assembly has been extracted from the bundle and is ready for analysis."
slug: api/dotsider.core.analysis.models.assemblyopenresult.bundleentry
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The file is a single-file bundle. The entry assembly has been extracted
from the bundle and is ready for analysis.

```csharp
public sealed record AssemblyOpenResult.BundleEntry : AssemblyOpenResult, IEquatable<AssemblyOpenResult>, IEquatable<AssemblyOpenResult.BundleEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/) → **AssemblyOpenResult.BundleEntry**

## Implements

- [IEquatable\<AssemblyOpenResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<BundleEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### BundleEntry(AssemblyAnalyzer, string)

The file is a single-file bundle. The entry assembly has been extracted
from the bundle and is ready for analysis.

**Parameters:**

- `EntryAnalyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer for the extracted entry assembly.
- `BundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Full path to the bundle file.

```csharp
public BundleEntry(AssemblyAnalyzer EntryAnalyzer, string BundlePath)
```

## Properties

### BundlePath

Full path to the bundle file.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string BundlePath { get; init; }
```

### EntryAnalyzer

The analyzer for the extracted entry assembly.

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer EntryAnalyzer { get; init; }
```

