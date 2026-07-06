---
title: "MstatSizeEntry"
description: "One normalized entry of an MstatSizeIndex: raw report rows aggregated under a build-stable identity key, with the structured hierarchy fields a consumer needs to place the entry in an assembly → namespace → type → leaf tree without parsing display strings."
slug: api/dotsider.core.analysis.models.mstatsizeentry
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One normalized entry of an [MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/): raw report
rows aggregated under a build-stable identity key, with the structured hierarchy fields a
consumer needs to place the entry in an assembly → namespace → type → leaf tree without
parsing display strings.

```csharp
public sealed record MstatSizeEntry : IEquatable<MstatSizeEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatSizeEntry**

## Implements

- [IEquatable\<MstatSizeEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatSizeEntry(MstatSectionKind, string, string, string, string, string, string, string, long, int, IReadOnlyList\<string\>)

One normalized entry of an [MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/): raw report
rows aggregated under a build-stable identity key, with the structured hierarchy fields a
consumer needs to place the entry in an assembly → namespace → type → leaf tree without
parsing display strings.

**Parameters:**

- `Section` ([MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/)): The report section the entry came from.
- `Key` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The build-stable identity key the entry's rows were aggregated under. Keys are comparable
across two builds of the same application; they are not display strings.
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly the bytes are attributed to. For frozen objects this is the owning type's
assembly — the code that caused the bytes — or
[UnattributedName](/api/dotsider.core.analysis.mstatsizeindex.unattributedname/) when the object has no
owner (string literals). Empty for global sections (blobs).
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The namespace the bytes are attributed to, an empty string for the global namespace, or
[UnattributedName](/api/dotsider.core.analysis.mstatsizeindex.unattributedname/) for ownerless frozen
objects. Blobs and resources carry no namespace.
- `TypeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The type-level grouping name (declaring type for methods, the type itself for MethodTables, the owning type for owned frozen objects), or an empty string for sections without one.
- `LeafName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The leaf display name, disambiguated where identity requires it (method names carry their parameter list).
- `DisplayName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The undecorated display name (a method's bare name, a blob's name).
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A deterministic, key-derived path for the entry, unique within the index.
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The aggregated size in bytes.
- `EntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of raw report rows folded into this entry. Greater than one means the entry is
an aggregate (overload display collisions, folded MethodTables, frozen objects grouped by
owner) and consumers must present it as such.
- `NodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Every dependency-graph node name behind the aggregated rows, in row order. These join to
DGML node labels and to native symbol names; an aggregate maps to as many nodes as it has
rows with names.

```csharp
public MstatSizeEntry(MstatSectionKind Section, string Key, string AssemblyName, string Namespace, string TypeName, string LeafName, string DisplayName, string FullPath, long Size, int EntryCount, IReadOnlyList<string> NodeNames)
```

## Properties

### AssemblyName

The assembly the bytes are attributed to. For frozen objects this is the owning type's
assembly — the code that caused the bytes — or
[UnattributedName](/api/dotsider.core.analysis.mstatsizeindex.unattributedname/) when the object has no
owner (string literals). Empty for global sections (blobs).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### DisplayName

The undecorated display name (a method's bare name, a blob's name).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DisplayName { get; init; }
```

### EntryCount

The number of raw report rows folded into this entry. Greater than one means the entry is
an aggregate (overload display collisions, folded MethodTables, frozen objects grouped by
owner) and consumers must present it as such.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int EntryCount { get; init; }
```

### FullPath

A deterministic, key-derived path for the entry, unique within the index.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullPath { get; init; }
```

### Key

The build-stable identity key the entry's rows were aggregated under. Keys are comparable
across two builds of the same application; they are not display strings.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Key { get; init; }
```

### LeafName

The leaf display name, disambiguated where identity requires it (method names carry their parameter list).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string LeafName { get; init; }
```

### Namespace

The namespace the bytes are attributed to, an empty string for the global namespace, or
[UnattributedName](/api/dotsider.core.analysis.mstatsizeindex.unattributedname/) for ownerless frozen
objects. Blobs and resources carry no namespace.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Namespace { get; init; }
```

### NodeNames

Every dependency-graph node name behind the aggregated rows, in row order. These join to
DGML node labels and to native symbol names; an aggregate maps to as many nodes as it has
rows with names.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> NodeNames { get; init; }
```

### Section

The report section the entry came from.

**Returns:** [MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/)

```csharp
public MstatSectionKind Section { get; init; }
```

### Size

The aggregated size in bytes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### TypeName

The type-level grouping name (declaring type for methods, the type itself for MethodTables, the owning type for owned frozen objects), or an empty string for sections without one.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string TypeName { get; init; }
```

