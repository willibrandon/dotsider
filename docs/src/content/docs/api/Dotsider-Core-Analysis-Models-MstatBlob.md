---
title: "MstatBlob"
description: "One named global data region from an ILC size report — embedded metadata, hydration tables, dispatch maps, and the like. Blob names come from the compiler's node type names (for example Metadata or InterfaceDispatchMap), with same-named regions summed into one entry."
slug: api/dotsider.core.analysis.models.mstatblob
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One named global data region from an ILC size report — embedded metadata, hydration
tables, dispatch maps, and the like. Blob names come from the compiler's node type names
(for example `Metadata` or `InterfaceDispatchMap`), with same-named regions
summed into one entry.

```csharp
public sealed record MstatBlob : IEquatable<MstatBlob>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatBlob**

## Implements

- [IEquatable\<MstatBlob\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatBlob(string, int)

One named global data region from an ILC size report — embedded metadata, hydration
tables, dispatch maps, and the like. Blob names come from the compiler's node type names
(for example `Metadata` or `InterfaceDispatchMap`), with same-named regions
summed into one entry.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The region name.
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The total size in bytes across all regions with this name.

```csharp
public MstatBlob(string Name, int Size)
```

## Properties

### Name

The region name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Size

The total size in bytes across all regions with this name.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

