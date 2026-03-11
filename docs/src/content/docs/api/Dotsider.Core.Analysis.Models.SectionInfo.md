---
title: "SectionInfo"
description: "Information about a single PE section (e.g., .text, .rsrc, .reloc)."
slug: api/dotsider.core.analysis.models.sectioninfo
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Information about a single PE section (e.g., .text, .rsrc, .reloc).

```csharp
public sealed record SectionInfo : IEquatable<SectionInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SectionInfo**

## Implements

- [IEquatable\<SectionInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SectionInfo(string, int, int, int, int, SectionCharacteristics)

Information about a single PE section (e.g., .text, .rsrc, .reloc).

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The section name (up to 8 characters).
- `VirtualAddress` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The RVA of the section when loaded into memory.
- `VirtualSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The size of the section in memory.
- `RawDataOffset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The file offset of the section's raw data.
- `RawDataSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The size of the section's raw data on disk.
- `Characteristics` ([SectionCharacteristics](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.sectioncharacteristics)): Section characteristic flags (readable, writable, executable, etc.).

```csharp
public SectionInfo(string Name, int VirtualAddress, int VirtualSize, int RawDataOffset, int RawDataSize, SectionCharacteristics Characteristics)
```

## Properties

### Characteristics

Section characteristic flags (readable, writable, executable, etc.).

**Returns:** [SectionCharacteristics](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.sectioncharacteristics)

```csharp
public SectionCharacteristics Characteristics { get; init; }
```

### Name

The section name (up to 8 characters).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### RawDataOffset

The file offset of the section's raw data.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RawDataOffset { get; init; }
```

### RawDataSize

The size of the section's raw data on disk.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RawDataSize { get; init; }
```

### VirtualAddress

The RVA of the section when loaded into memory.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int VirtualAddress { get; init; }
```

### VirtualSize

The size of the section in memory.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int VirtualSize { get; init; }
```

