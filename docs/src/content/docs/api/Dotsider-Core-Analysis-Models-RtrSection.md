---
title: "RtrSection"
description: "One entry in a Native AOT binary's ReadyToRun section table. Each section describes a runtime data region — frozen objects, GC statics, dehydrated data, or a readonly blob such as the embedded metadata — the way an ECMA-335 table describes a managed assembly."
slug: api/dotsider.core.analysis.models.rtrsection
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One entry in a Native AOT binary's ReadyToRun section table. Each section describes a
runtime data region — frozen objects, GC statics, dehydrated data, or a readonly blob
such as the embedded metadata — the way an ECMA-335 table describes a managed assembly.

```csharp
public sealed record RtrSection : IEquatable<RtrSection>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **RtrSection**

## Implements

- [IEquatable\<RtrSection\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### RtrSection(int, string, ulong, long, int?)

One entry in a Native AOT binary's ReadyToRun section table. Each section describes a
runtime data region — frozen objects, GC statics, dehydrated data, or a readonly blob
such as the embedded metadata — the way an ECMA-335 table describes a managed assembly.

**Parameters:**

- `SectionId` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The `ReadyToRunSectionType` id (e.g. 206 = FrozenObjectRegion).
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A human-readable name for the section id.
- `VirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The section's absolute virtual address.
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The section size in bytes, or 0 when the header does not record it.
- `FileOffset` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The file offset the virtual address maps to, or null when the section exists only in
memory (for example an ELF NOBITS region that the runtime fills at startup).

```csharp
public RtrSection(int SectionId, string Name, ulong VirtualAddress, long Size, int? FileOffset)
```

## Properties

### FileOffset

The file offset the virtual address maps to, or null when the section exists only in
memory (for example an ELF NOBITS region that the runtime fills at startup).

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? FileOffset { get; init; }
```

### Name

A human-readable name for the section id.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### SectionId

The `ReadyToRunSectionType` id (e.g. 206 = FrozenObjectRegion).

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SectionId { get; init; }
```

### Size

The section size in bytes, or 0 when the header does not record it.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### VirtualAddress

The section's absolute virtual address.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong VirtualAddress { get; init; }
```

