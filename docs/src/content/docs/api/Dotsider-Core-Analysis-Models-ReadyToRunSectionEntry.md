---
title: "ReadyToRunSectionEntry"
description: "One row of a crossgen2 image's READYTORUN_SECTION table: a section type and the {RVA, Size} data directory that locates it. Rendered by the PE/Metadata \"R2R Sections\" tab for ReadyToRun images."
slug: api/dotsider.core.analysis.models.readytorunsectionentry
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One row of a crossgen2 image's `READYTORUN_SECTION` table: a section type and the
`{RVA, Size}` data directory that locates it. Rendered by the PE/Metadata "R2R Sections"
tab for ReadyToRun images.

```csharp
public sealed record ReadyToRunSectionEntry : IEquatable<ReadyToRunSectionEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunSectionEntry**

## Implements

- [IEquatable\<ReadyToRunSectionEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ReadyToRunSectionEntry(int, string, int, int, int?)

One row of a crossgen2 image's `READYTORUN_SECTION` table: a section type and the
`{RVA, Size}` data directory that locates it. Rendered by the PE/Metadata "R2R Sections"
tab for ReadyToRun images.

**Parameters:**

- `Type` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The raw `ReadyToRunSectionType` id.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A human-readable name for the section type.
- `Rva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The section's relative virtual address.
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The section size in bytes.
- `FileOffset` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The file offset the RVA maps to, or null when it is not file-backed.

```csharp
public ReadyToRunSectionEntry(int Type, string Name, int Rva, int Size, int? FileOffset)
```

## Properties

### FileOffset

The file offset the RVA maps to, or null when it is not file-backed.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? FileOffset { get; init; }
```

### Name

A human-readable name for the section type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Rva

The section's relative virtual address.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Rva { get; init; }
```

### Size

The section size in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

### Type

The raw `ReadyToRunSectionType` id.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Type { get; init; }
```

## Methods

### Deconstruct(out int, out string, out int, out int, out int?)

**Parameters:**

- `Type` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Rva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `FileOffset` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public void Deconstruct(out int Type, out string Name, out int Rva, out int Size, out int? FileOffset)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(ReadyToRunSectionEntry?)

**Parameters:**

- `other` ([ReadyToRunSectionEntry](/api/dotsider.core.analysis.models.readytorunsectionentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ReadyToRunSectionEntry? other)
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

### operator !=(ReadyToRunSectionEntry?, ReadyToRunSectionEntry?)

**Parameters:**

- `left` ([ReadyToRunSectionEntry](/api/dotsider.core.analysis.models.readytorunsectionentry/))
- `right` ([ReadyToRunSectionEntry](/api/dotsider.core.analysis.models.readytorunsectionentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ReadyToRunSectionEntry? left, ReadyToRunSectionEntry? right)
```

### operator ==(ReadyToRunSectionEntry?, ReadyToRunSectionEntry?)

**Parameters:**

- `left` ([ReadyToRunSectionEntry](/api/dotsider.core.analysis.models.readytorunsectionentry/))
- `right` ([ReadyToRunSectionEntry](/api/dotsider.core.analysis.models.readytorunsectionentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ReadyToRunSectionEntry? left, ReadyToRunSectionEntry? right)
```
