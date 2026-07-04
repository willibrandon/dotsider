---
title: "AssemblyOpenResult"
description: "The result of opening an assembly file via AssemblyLoader, distinguishing between direct loads, apphost companion redirects, and single-file bundle entry extractions."
slug: api/dotsider.core.analysis.models.assemblyopenresult
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The result of opening an assembly file via [AssemblyLoader](/api/dotsider.core.analysis.assemblyloader/),
distinguishing between direct loads, apphost companion redirects, and
single-file bundle entry extractions.

```csharp
public abstract record AssemblyOpenResult : IEquatable<AssemblyOpenResult>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyOpenResult**

## Implements

- [IEquatable\<AssemblyOpenResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

