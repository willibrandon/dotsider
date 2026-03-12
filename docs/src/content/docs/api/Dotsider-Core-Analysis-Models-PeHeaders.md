---
title: "PeHeaders"
description: "Aggregated PE header information for a .NET assembly."
slug: api/dotsider.core.analysis.models.peheaders
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Aggregated PE header information for a .NET assembly.

```csharp
public sealed record PeHeaders : IEquatable<PeHeaders>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **PeHeaders**

## Implements

- [IEquatable\<PeHeaders\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### PeHeaders(Machine, Characteristics, int, PEMagic, byte, byte, int, int, ulong, int, int, int, int, Subsystem, DllCharacteristics, int)

Aggregated PE header information for a .NET assembly.

**Parameters:**

- `Machine` ([Machine](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.machine)): The target machine architecture.
- `Characteristics` ([Characteristics](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.characteristics)): COFF header characteristics flags.
- `TimeDateStamp` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The linker timestamp from the COFF header.
- `Magic` ([PEMagic](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.pemagic)): PE magic number (PE32 or PE32+).
- `MajorLinkerVersion` ([Byte](https://learn.microsoft.com/dotnet/api/system.byte)): Major version of the linker that produced the image.
- `MinorLinkerVersion` ([Byte](https://learn.microsoft.com/dotnet/api/system.byte)): Minor version of the linker that produced the image.
- `SizeOfCode` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Total size of all code sections.
- `EntryPointRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): RVA of the entry point function.
- `ImageBase` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): Preferred base address of the image.
- `SectionAlignment` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Alignment of sections in memory.
- `FileAlignment` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Alignment of sections on disk.
- `SizeOfImage` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Total size of the image in memory.
- `SizeOfHeaders` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Combined size of all headers.
- `Subsystem` ([Subsystem](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.subsystem)): The Windows subsystem required to run the image.
- `DllCharacteristics` ([DllCharacteristics](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.dllcharacteristics)): DLL characteristics flags (ASLR, DEP, etc.).
- `NumberOfSections` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of sections in the PE file.

```csharp
public PeHeaders(Machine Machine, Characteristics Characteristics, int TimeDateStamp, PEMagic Magic, byte MajorLinkerVersion, byte MinorLinkerVersion, int SizeOfCode, int EntryPointRva, ulong ImageBase, int SectionAlignment, int FileAlignment, int SizeOfImage, int SizeOfHeaders, Subsystem Subsystem, DllCharacteristics DllCharacteristics, int NumberOfSections)
```

## Properties

### Characteristics

COFF header characteristics flags.

**Returns:** [Characteristics](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.characteristics)

```csharp
public Characteristics Characteristics { get; init; }
```

### DllCharacteristics

DLL characteristics flags (ASLR, DEP, etc.).

**Returns:** [DllCharacteristics](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.dllcharacteristics)

```csharp
public DllCharacteristics DllCharacteristics { get; init; }
```

### EntryPointRva

RVA of the entry point function.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int EntryPointRva { get; init; }
```

### FileAlignment

Alignment of sections on disk.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int FileAlignment { get; init; }
```

### ImageBase

Preferred base address of the image.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong ImageBase { get; init; }
```

### Machine

The target machine architecture.

**Returns:** [Machine](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.machine)

```csharp
public Machine Machine { get; init; }
```

### Magic

PE magic number (PE32 or PE32+).

**Returns:** [PEMagic](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.pemagic)

```csharp
public PEMagic Magic { get; init; }
```

### MajorLinkerVersion

Major version of the linker that produced the image.

**Returns:** [Byte](https://learn.microsoft.com/dotnet/api/system.byte)

```csharp
public byte MajorLinkerVersion { get; init; }
```

### MinorLinkerVersion

Minor version of the linker that produced the image.

**Returns:** [Byte](https://learn.microsoft.com/dotnet/api/system.byte)

```csharp
public byte MinorLinkerVersion { get; init; }
```

### NumberOfSections

Number of sections in the PE file.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int NumberOfSections { get; init; }
```

### SectionAlignment

Alignment of sections in memory.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SectionAlignment { get; init; }
```

### SizeOfCode

Total size of all code sections.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SizeOfCode { get; init; }
```

### SizeOfHeaders

Combined size of all headers.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SizeOfHeaders { get; init; }
```

### SizeOfImage

Total size of the image in memory.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SizeOfImage { get; init; }
```

### Subsystem

The Windows subsystem required to run the image.

**Returns:** [Subsystem](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.subsystem)

```csharp
public Subsystem Subsystem { get; init; }
```

### TimeDateStamp

The linker timestamp from the COFF header.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TimeDateStamp { get; init; }
```

