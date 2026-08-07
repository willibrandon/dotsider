---
title: "MstatData"
description: "The contents of an ILC size report (.mstat), produced by publishing a Native AOT project with IlcGenerateMstatFile. The file is itself a valid ECMA-335 assembly whose assembly version carries the format version and whose data lives in IL streams; this record is the decoded result. Sections absent from older format versions are empty lists."
slug: api/dotsider.core.analysis.models.mstatdata
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The contents of an ILC size report (`.mstat`), produced by publishing a Native AOT
project with `IlcGenerateMstatFile`. The file is itself a valid ECMA-335 assembly whose
assembly version carries the format version and whose data lives in IL streams; this record
is the decoded result. Sections absent from older format versions are empty lists.

```csharp
public sealed record MstatData : IEquatable<MstatData>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatData**

## Implements

- [IEquatable\<MstatData\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatData(int, int, IReadOnlyList\<AssemblyRefInfo\>, IReadOnlyList\<MstatMethod\>, IReadOnlyList\<MstatType\>, IReadOnlyList\<MstatBlob\>, IReadOnlyList\<MstatRvaField\>, IReadOnlyList\<MstatFrozenObject\>, IReadOnlyList\<MstatManifestResource\>, IReadOnlyList\<MstatDeduplicatedMethod\>)

The contents of an ILC size report (`.mstat`), produced by publishing a Native AOT
project with `IlcGenerateMstatFile`. The file is itself a valid ECMA-335 assembly whose
assembly version carries the format version and whose data lives in IL streams; this record
is the decoded result. Sections absent from older format versions are empty lists.

**Parameters:**

- `FormatMajorVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The format major version (1 = .NET 7, 2 = .NET 8+).
- `FormatMinorVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The format minor version (2.1 adds RVA field, frozen object, and resource detail; 2.2 adds deduplicated methods).
- `Assemblies` ([IReadOnlyList\<AssemblyRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The managed assemblies the report references, in AssemblyRef table order.
- `Methods` ([IReadOnlyList\<MstatMethod\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Every compiled method body with its code, GC info, and EH info sizes.
- `Types` ([IReadOnlyList\<MstatType\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Every constructed MethodTable with its size.
- `Blobs` ([IReadOnlyList\<MstatBlob\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Global data regions (metadata, dehydrated data, hydration tables) by name.
- `RvaFields` ([IReadOnlyList\<MstatRvaField\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Field RVA data entries (format 2.1+); their bytes also appear in Blobs for back-compat.
- `FrozenObjects` ([IReadOnlyList\<MstatFrozenObject\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Frozen object entries (format 2.1+); their bytes also appear in Blobs for back-compat.
- `ManifestResources` ([IReadOnlyList\<MstatManifestResource\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Embedded manifest resources (format 2.1+); their bytes also appear in Blobs for back-compat.
- `DeduplicatedMethods` ([IReadOnlyList\<MstatDeduplicatedMethod\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Method bodies folded into an identical original (format 2.2+).

```csharp
public MstatData(int FormatMajorVersion, int FormatMinorVersion, IReadOnlyList<AssemblyRefInfo> Assemblies, IReadOnlyList<MstatMethod> Methods, IReadOnlyList<MstatType> Types, IReadOnlyList<MstatBlob> Blobs, IReadOnlyList<MstatRvaField> RvaFields, IReadOnlyList<MstatFrozenObject> FrozenObjects, IReadOnlyList<MstatManifestResource> ManifestResources, IReadOnlyList<MstatDeduplicatedMethod> DeduplicatedMethods)
```

## Properties

### Assemblies

The managed assemblies the report references, in AssemblyRef table order.

**Returns:** [IReadOnlyList\<AssemblyRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<AssemblyRefInfo> Assemblies { get; init; }
```

### Blobs

Global data regions (metadata, dehydrated data, hydration tables) by name.

**Returns:** [IReadOnlyList\<MstatBlob\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatBlob> Blobs { get; init; }
```

### DeduplicatedMethods

Method bodies folded into an identical original (format 2.2+).

**Returns:** [IReadOnlyList\<MstatDeduplicatedMethod\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatDeduplicatedMethod> DeduplicatedMethods { get; init; }
```

### Empty

A report with no entries and format version 0.0 — the baseline for size-budget checks
that run without one, where every entry of the build under check diffs as added.

**Returns:** [MstatData](/api/dotsider.core.analysis.models.mstatdata/)

```csharp
public static MstatData Empty { get; }
```

### FormatMajorVersion

The format major version (1 = .NET 7, 2 = .NET 8+).

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int FormatMajorVersion { get; init; }
```

### FormatMinorVersion

The format minor version (2.1 adds RVA field, frozen object, and resource detail; 2.2 adds deduplicated methods).

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int FormatMinorVersion { get; init; }
```

### FrozenObjects

Frozen object entries (format 2.1+); their bytes also appear in Blobs for back-compat.

**Returns:** [IReadOnlyList\<MstatFrozenObject\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatFrozenObject> FrozenObjects { get; init; }
```

### ManifestResources

Embedded manifest resources (format 2.1+); their bytes also appear in Blobs for back-compat.

**Returns:** [IReadOnlyList\<MstatManifestResource\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatManifestResource> ManifestResources { get; init; }
```

### Methods

Every compiled method body with its code, GC info, and EH info sizes.

**Returns:** [IReadOnlyList\<MstatMethod\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatMethod> Methods { get; init; }
```

### RvaFields

Field RVA data entries (format 2.1+); their bytes also appear in Blobs for back-compat.

**Returns:** [IReadOnlyList\<MstatRvaField\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatRvaField> RvaFields { get; init; }
```

### Types

Every constructed MethodTable with its size.

**Returns:** [IReadOnlyList\<MstatType\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatType> Types { get; init; }
```

## Methods

### Deconstruct(out int, out int, out IReadOnlyList\<AssemblyRefInfo\>, out IReadOnlyList\<MstatMethod\>, out IReadOnlyList\<MstatType\>, out IReadOnlyList\<MstatBlob\>, out IReadOnlyList\<MstatRvaField\>, out IReadOnlyList\<MstatFrozenObject\>, out IReadOnlyList\<MstatManifestResource\>, out IReadOnlyList\<MstatDeduplicatedMethod\>)

**Parameters:**

- `FormatMajorVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `FormatMinorVersion` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Assemblies` ([IReadOnlyList\<AssemblyRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Methods` ([IReadOnlyList\<MstatMethod\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Types` ([IReadOnlyList\<MstatType\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Blobs` ([IReadOnlyList\<MstatBlob\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `RvaFields` ([IReadOnlyList\<MstatRvaField\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `FrozenObjects` ([IReadOnlyList\<MstatFrozenObject\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `ManifestResources` ([IReadOnlyList\<MstatManifestResource\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `DeduplicatedMethods` ([IReadOnlyList\<MstatDeduplicatedMethod\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out int FormatMajorVersion, out int FormatMinorVersion, out IReadOnlyList<AssemblyRefInfo> Assemblies, out IReadOnlyList<MstatMethod> Methods, out IReadOnlyList<MstatType> Types, out IReadOnlyList<MstatBlob> Blobs, out IReadOnlyList<MstatRvaField> RvaFields, out IReadOnlyList<MstatFrozenObject> FrozenObjects, out IReadOnlyList<MstatManifestResource> ManifestResources, out IReadOnlyList<MstatDeduplicatedMethod> DeduplicatedMethods)
```

### Equals(MstatData?)

**Parameters:**

- `other` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatData? other)
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

### operator !=(MstatData?, MstatData?)

**Parameters:**

- `left` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/))
- `right` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatData? left, MstatData? right)
```

### operator ==(MstatData?, MstatData?)

**Parameters:**

- `left` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/))
- `right` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatData? left, MstatData? right)
```
