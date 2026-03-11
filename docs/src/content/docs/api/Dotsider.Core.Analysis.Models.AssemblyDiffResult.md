---
title: "AssemblyDiffResult"
description: "The complete diff result between two assemblies."
slug: api/dotsider.core.analysis.models.assemblydiffresult
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

- `TypeDiffs` ([IReadOnlyList\<TypeDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): 
- `MethodDiffs` ([IReadOnlyList\<MethodDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): 
- `AssemblyRefDiffs` ([IReadOnlyList\<AssemblyRefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): 
- `MetadataSummary` ([DiffSummary](/api/dotsider.core.analysis.models.diffsummary/)): 

```csharp
public AssemblyDiffResult(IReadOnlyList<DiffEntry<TypeDefInfo>> TypeDiffs, IReadOnlyList<DiffEntry<MethodDefInfo>> MethodDiffs, IReadOnlyList<DiffEntry<AssemblyRefInfo>> AssemblyRefDiffs, DiffSummary MetadataSummary)
```

## Properties

### AssemblyRefDiffs

**Returns:** [IReadOnlyList\<AssemblyRefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DiffEntry<AssemblyRefInfo>> AssemblyRefDiffs { get; init; }
```

### MetadataSummary

**Returns:** [DiffSummary](/api/dotsider.core.analysis.models.diffsummary/)

```csharp
public DiffSummary MetadataSummary { get; init; }
```

### MethodDiffs

**Returns:** [IReadOnlyList\<MethodDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DiffEntry<MethodDefInfo>> MethodDiffs { get; init; }
```

### TypeDiffs

**Returns:** [IReadOnlyList\<TypeDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DiffEntry<TypeDefInfo>> TypeDiffs { get; init; }
```

