---
title: "SizeAnalyzer"
description: "Computes IL code size per method and builds a hierarchical size tree for treemap visualization. For a Native AOT binary with an mstat sidecar the tree is built from the compiler's size report instead: native code and MethodTable bytes per assembly, namespace, type, and method, plus the binary's data categories."
slug: api/dotsider.core.analysis.sizeanalyzer
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Computes IL code size per method and builds a hierarchical size tree
for treemap visualization. For a Native AOT binary with an mstat sidecar the tree is
built from the compiler's size report instead: native code and MethodTable bytes per
assembly, namespace, type, and method, plus the binary's data categories.

```csharp
public static class SizeAnalyzer
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeAnalyzer**

## Methods

### BuildSizeTree(AssemblyAnalyzer)

Builds a hierarchical size tree from the assembly's methods grouped by type and namespace.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The assembly analyzer to read method metadata from.

**Returns:** [SizeNode](/api/dotsider.core.analysis.models.sizenode/)

The root [SizeNode](/api/dotsider.core.analysis.models.sizenode/) representing the entire assembly.

```csharp
public static SizeNode BuildSizeTree(AssemblyAnalyzer analyzer)
```

