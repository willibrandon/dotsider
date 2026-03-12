---
title: "AssemblyDiffer"
description: "Compares two assemblies and produces a detailed diff result. Uses dictionary-based O(n) matching by name."
slug: api/dotsider.core.analysis.assemblydiffer
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Compares two assemblies and produces a detailed diff result.
Uses dictionary-based O(n) matching by name.

```csharp
public static class AssemblyDiffer
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyDiffer**

## Methods

### Compare(AssemblyAnalyzer, AssemblyAnalyzer)

Compares two assemblies and returns a structured diff result.

**Parameters:**

- `left` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The baseline assembly.
- `right` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The changed assembly.

**Returns:** [AssemblyDiffResult](/api/dotsider.core.analysis.models.assemblydiffresult/)

A diff result containing type, method, and reference differences.

```csharp
public static AssemblyDiffResult Compare(AssemblyAnalyzer left, AssemblyAnalyzer right)
```

