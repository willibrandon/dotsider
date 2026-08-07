---
title: "DebugDirectoryInfo"
description: "Display-ready PE debug directory entry information."
slug: api/dotsider.core.analysis.models.debugdirectoryinfo
sidebar:
  order: 2
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

## Methods

### Deconstruct(out DebugDirectoryEntryType, out uint, out ushort, out ushort, out int, out int, out int, out string)

**Parameters:**

- `Type` ([DebugDirectoryEntryType](https://learn.microsoft.com/dotnet/api/system.reflection.portableexecutable.debugdirectoryentrytype))
- `Stamp` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32))
- `MajorVersion` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16))
- `MinorVersion` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16))
- `DataSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `AddressOfRawData` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `PointerToRawData` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Payload` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out DebugDirectoryEntryType Type, out uint Stamp, out ushort MajorVersion, out ushort MinorVersion, out int DataSize, out int AddressOfRawData, out int PointerToRawData, out string Payload)
```

### Equals(DebugDirectoryInfo?)

**Parameters:**

- `other` ([DebugDirectoryInfo](/api/dotsider.core.analysis.models.debugdirectoryinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(DebugDirectoryInfo? other)
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

### operator !=(DebugDirectoryInfo?, DebugDirectoryInfo?)

**Parameters:**

- `left` ([DebugDirectoryInfo](/api/dotsider.core.analysis.models.debugdirectoryinfo/))
- `right` ([DebugDirectoryInfo](/api/dotsider.core.analysis.models.debugdirectoryinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(DebugDirectoryInfo? left, DebugDirectoryInfo? right)
```

### operator ==(DebugDirectoryInfo?, DebugDirectoryInfo?)

**Parameters:**

- `left` ([DebugDirectoryInfo](/api/dotsider.core.analysis.models.debugdirectoryinfo/))
- `right` ([DebugDirectoryInfo](/api/dotsider.core.analysis.models.debugdirectoryinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(DebugDirectoryInfo? left, DebugDirectoryInfo? right)
```
