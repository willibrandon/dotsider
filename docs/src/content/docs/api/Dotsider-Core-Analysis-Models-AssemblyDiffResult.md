---
title: "AssemblyDiffResult"
description: "The complete diff result between two assemblies."
slug: api/dotsider.core.analysis.models.assemblydiffresult
sidebar:
  order: 2
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

## Methods

### Deconstruct(out IReadOnlyList\<DiffEntry\<TypeDefInfo\>\>, out IReadOnlyList\<DiffEntry\<MethodDefInfo\>\>, out IReadOnlyList\<DiffEntry\<AssemblyRefInfo\>\>, out DiffSummary)

**Parameters:**

- `TypeDiffs` ([IReadOnlyList\<TypeDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `MethodDiffs` ([IReadOnlyList\<MethodDefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `AssemblyRefDiffs` ([IReadOnlyList\<AssemblyRefInfo\>\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `MetadataSummary` ([DiffSummary](/api/dotsider.core.analysis.models.diffsummary/))

```csharp
public void Deconstruct(out IReadOnlyList<DiffEntry<TypeDefInfo>> TypeDiffs, out IReadOnlyList<DiffEntry<MethodDefInfo>> MethodDiffs, out IReadOnlyList<DiffEntry<AssemblyRefInfo>> AssemblyRefDiffs, out DiffSummary MetadataSummary)
```

### Equals(AssemblyDiffResult?)

**Parameters:**

- `other` ([AssemblyDiffResult](/api/dotsider.core.analysis.models.assemblydiffresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(AssemblyDiffResult? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(AssemblyDiffResult?, AssemblyDiffResult?)

**Parameters:**

- `left` ([AssemblyDiffResult](/api/dotsider.core.analysis.models.assemblydiffresult/))
- `right` ([AssemblyDiffResult](/api/dotsider.core.analysis.models.assemblydiffresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(AssemblyDiffResult? left, AssemblyDiffResult? right)
```

### operator ==(AssemblyDiffResult?, AssemblyDiffResult?)

**Parameters:**

- `left` ([AssemblyDiffResult](/api/dotsider.core.analysis.models.assemblydiffresult/))
- `right` ([AssemblyDiffResult](/api/dotsider.core.analysis.models.assemblydiffresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(AssemblyDiffResult? left, AssemblyDiffResult? right)
```
