---
title: "AssemblyDiffResult"
description: "The complete diff result between two assemblies."
slug: api/dotsider.core.analysis.models.assemblydiffresult
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The complete diff result between two assemblies.

```csharp
public sealed record AssemblyDiffResult : IEquatable<AssemblyDiffResult>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyDiffResult**

## Implements

- [IEquatable\<AssemblyDiffResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### AssemblyDiffResult(IReadOnlyList\<DiffEntry\<TypeDefInfo\>\>, IReadOnlyList\<DiffEntry\<MethodDefInfo\>\>, IReadOnlyList\<DiffEntry\<AssemblyRefInfo\>\>, DiffSummary)

The complete diff result between two assemblies.

**Parameters:**

- `TypeDiffs` ([IReadOnlyList\<TypeDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Diff entries for type definitions.
- `MethodDiffs` ([IReadOnlyList\<MethodDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Diff entries for method definitions.
- `AssemblyRefDiffs` ([IReadOnlyList\<AssemblyRefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Diff entries for assembly references.
- `MetadataSummary` ([DiffSummary](/api/dotsider.core.analysis.models.diffsummary/)): Aggregate counts of added, removed, and changed items.

```csharp
public AssemblyDiffResult(IReadOnlyList<DiffEntry<TypeDefInfo>> TypeDiffs, IReadOnlyList<DiffEntry<MethodDefInfo>> MethodDiffs, IReadOnlyList<DiffEntry<AssemblyRefInfo>> AssemblyRefDiffs, DiffSummary MetadataSummary)
```

## Properties

### AssemblyRefDiffs

Diff entries for assembly references.

**Returns:** [IReadOnlyList\<AssemblyRefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DiffEntry<AssemblyRefInfo>> AssemblyRefDiffs { get; init; }
```

### MetadataSummary

Aggregate counts of added, removed, and changed items.

**Returns:** [DiffSummary](/api/dotsider.core.analysis.models.diffsummary/)

```csharp
public DiffSummary MetadataSummary { get; init; }
```

### MethodDiffs

Diff entries for method definitions.

**Returns:** [IReadOnlyList\<MethodDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DiffEntry<MethodDefInfo>> MethodDiffs { get; init; }
```

### TypeDiffs

Diff entries for type definitions.

**Returns:** [IReadOnlyList\<TypeDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DiffEntry<TypeDefInfo>> TypeDiffs { get; init; }
```

