---
title: "DebugDirectoryInfo"
description: "Display-ready PE debug directory entry information."
slug: api/dotsider.core.analysis.models.debugdirectoryinfo
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Display-ready PE debug directory entry information.

```csharp
public sealed record DebugDirectoryInfo : IEquatable<DebugDirectoryInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DebugDirectoryInfo**

## Implements

- [IEquatable\<DebugDirectoryInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### DebugDirectoryInfo(DebugDirectoryEntryType, uint, ushort, ushort, int, int, int, string)

Display-ready PE debug directory entry information.

**Parameters:**

- `Type` ([DebugDirectoryEntryType](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.debugdirectoryentrytype)): The debug directory entry type.
- `Stamp` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): The entry stamp.
- `MajorVersion` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)): The major debug format version.
- `MinorVersion` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)): The minor debug format version.
- `DataSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The payload size in bytes.
- `AddressOfRawData` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The payload RVA.
- `PointerToRawData` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The payload file pointer.
- `Payload` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Inline payload summary for known entry types.

```csharp
public DebugDirectoryInfo(DebugDirectoryEntryType Type, uint Stamp, ushort MajorVersion, ushort MinorVersion, int DataSize, int AddressOfRawData, int PointerToRawData, string Payload)
```

## Properties

### AddressOfRawData

The payload RVA.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int AddressOfRawData { get; init; }
```

### DataSize

The payload size in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int DataSize { get; init; }
```

### MajorVersion

The major debug format version.

**Returns:** [UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)

```csharp
public ushort MajorVersion { get; init; }
```

### MinorVersion

The minor debug format version.

**Returns:** [UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)

```csharp
public ushort MinorVersion { get; init; }
```

### Payload

Inline payload summary for known entry types.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Payload { get; init; }
```

### PointerToRawData

The payload file pointer.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int PointerToRawData { get; init; }
```

### Stamp

The entry stamp.

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint Stamp { get; init; }
```

### Type

The debug directory entry type.

**Returns:** [DebugDirectoryEntryType](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.debugdirectoryentrytype)

```csharp
public DebugDirectoryEntryType Type { get; init; }
```

