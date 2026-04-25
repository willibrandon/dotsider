---
title: "LoadedAssemblyEntry"
description: "Per-loaded-identity entry interned in LoadedAssemblyCache. When two distinct requested identities redirect to the same loaded identity, both Loaded values reference-equal this single entry, faithfully modeling the CLR's \"already loaded\" reuse: only one filesystem read per loaded identity."
slug: api/dotsider.core.analysis.models.loadedassemblyentry
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Per-loaded-identity entry interned in `LoadedAssemblyCache`. When two distinct requested
identities redirect to the same loaded identity, both [Loaded](/api/dotsider.core.analysis.models.netfxbindresult.loaded/)
values reference-equal this single entry, faithfully modeling the CLR's "already loaded"
reuse: only one filesystem read per loaded identity.

```csharp
public sealed record LoadedAssemblyEntry : IEquatable<LoadedAssemblyEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **LoadedAssemblyEntry**

## Implements

- [IEquatable\<LoadedAssemblyEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### LoadedAssemblyEntry(AssemblyRefInfo, string)

Per-loaded-identity entry interned in `LoadedAssemblyCache`. When two distinct requested
identities redirect to the same loaded identity, both [Loaded](/api/dotsider.core.analysis.models.netfxbindresult.loaded/)
values reference-equal this single entry, faithfully modeling the CLR's "already loaded"
reuse: only one filesystem read per loaded identity.

**Parameters:**

- `Identity` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): The bound identity (post-policy) that this entry represents.
- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The on-disk file path the CLR would load for this identity.

```csharp
public LoadedAssemblyEntry(AssemblyRefInfo Identity, string Path)
```

## Properties

### Identity

The bound identity (post-policy) that this entry represents.

**Returns:** [AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)

```csharp
public AssemblyRefInfo Identity { get; init; }
```

### Path

The on-disk file path the CLR would load for this identity.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Path { get; init; }
```

