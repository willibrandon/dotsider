---
title: "ReadyToRunIndex"
description: "Queryable view of a ReadyToRun image's precompiled methods: managed-method lookup by owning assembly identity and token, and reverse lookup by native address over the methods' disjoint code ranges. The token is qualified by assembly name because a composite spans several assemblies whose tokens collide. Built once, every lookup a dictionary or binary-search hit."
slug: api/dotsider.core.analysis.readytorunindex
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Queryable view of a ReadyToRun image's precompiled methods: managed-method lookup by owning
assembly identity and token, and reverse lookup by native address over the methods' disjoint
code ranges. The token is qualified by assembly name because a composite spans several
assemblies whose tokens collide. Built once, every lookup a dictionary or binary-search hit.

```csharp
public sealed class ReadyToRunIndex
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunIndex**

## Properties

### InstantiationCount

The number of generic-instantiation entries.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int InstantiationCount { get; }
```

### Methods

Every precompiled method entry (base methods and generic instantiations).

**Returns:** [IReadOnlyList\<ReadyToRunMethodEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ReadyToRunMethodEntry> Methods { get; }
```

### TotalCodeSize

The total precompiled native code size across all methods.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long TotalCodeSize { get; }
```

## Methods

### Build(IReadOnlyList\<ReadyToRunMethodEntry\>)

Builds the index from a ReadyToRun image's method entries.

**Parameters:**

- `methods` ([IReadOnlyList\<ReadyToRunMethodEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The precompiled method entries.

**Returns:** [ReadyToRunIndex](/api/dotsider.core.analysis.readytorunindex/)

```csharp
public static ReadyToRunIndex Build(IReadOnlyList<ReadyToRunMethodEntry> methods)
```

### Find(string, int)

Finds a method's primary (non-generic) entry by owning assembly name and token, or the
first entry when only instantiations exist.

**Parameters:**

- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The owning assembly's simple name.
- `token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The method's metadata token.

**Returns:** [ReadyToRunMethodEntry](/api/dotsider.core.analysis.models.readytorunmethodentry/)

```csharp
public ReadyToRunMethodEntry? Find(string assemblyName, int token)
```

### FindAll(string, int)

Every entry for a token — the base method plus any generic instantiations.

**Parameters:**

- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The owning assembly's simple name.
- `token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The method's metadata token.

**Returns:** [IReadOnlyList\<ReadyToRunMethodEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ReadyToRunMethodEntry> FindAll(string assemblyName, int token)
```

### FindByAddress(ulong)

Finds the method whose native code covers virtualAddress — an address
anywhere inside any of its ranges — or null for uncorrelated (helper/stub) code.

**Parameters:**

- `virtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): A virtual address, e.g. a call target.

**Returns:** [ReadyToRunMethodEntry](/api/dotsider.core.analysis.models.readytorunmethodentry/)

```csharp
public ReadyToRunMethodEntry? FindByAddress(ulong virtualAddress)
```
