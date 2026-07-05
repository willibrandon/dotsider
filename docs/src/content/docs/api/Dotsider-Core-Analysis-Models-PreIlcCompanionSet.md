---
title: "PreIlcCompanionSet"
description: "The attached pre-ILC companions of a Native AOT binary: the root managed input and any validated local reference assemblies."
slug: api/dotsider.core.analysis.models.preilccompanionset
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The attached pre-ILC companions of a Native AOT binary: the root managed input and any
validated local reference assemblies.

```csharp
public sealed class PreIlcCompanionSet
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **PreIlcCompanionSet**

## Properties

### All

The root followed by the local references.

**Returns:** [IReadOnlyList\<AssemblyAnalyzer\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<AssemblyAnalyzer> All { get; }
```

### LocalReferences

Local/project reference assemblies that also fed the compilation, validated on attach.

**Returns:** [IReadOnlyList\<AssemblyAnalyzer\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<AssemblyAnalyzer> LocalReferences { get; }
```

### Root

The root managed input — the assembly ILC compiled. Metadata surfaces route here first.

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer Root { get; }
```

## Methods

### FindByAssemblyName(string)

Finds a member of the set by assembly simple name, or null.

**Parameters:**

- `name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly simple name to look for.

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer? FindByAssemblyName(string name)
```

## Remarks

Ownership: the set and every analyzer in it are owned by the
[AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/) they were attached to. Consumers must never dispose
[Root](/api/dotsider.core.analysis.models.preilccompanionset.root/) or [LocalReferences](/api/dotsider.core.analysis.models.preilccompanionset.localreferences/) — they become invalid when the
owner detaches or is disposed. The type deliberately does not implement
[IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable); teardown is internal to the owning analyzer.

