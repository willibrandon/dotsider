---
title: "NativeAotInfo"
description: "Facts extracted from the embedded ReadyToRun header of a Native AOT binary. Every Native AOT image embeds this header (signature \"RTR\\0\") so the runtime can locate its module sections; its presence with no COR header identifies the binary as Native AOT compiled .NET."
slug: api/dotsider.core.analysis.models.nativeaotinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Facts extracted from the embedded ReadyToRun header of a Native AOT binary.
Every Native AOT image embeds this header (signature "RTR\0") so the runtime can
locate its module sections; its presence with no COR header identifies the binary
as Native AOT compiled .NET.

```csharp
public sealed record NativeAotInfo : IEquatable<NativeAotInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeAotInfo**

## Implements

- [IEquatable\<NativeAotInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeAotInfo(int, ushort, ushort, uint, int, byte, string?)

Facts extracted from the embedded ReadyToRun header of a Native AOT binary.
Every Native AOT image embeds this header (signature "RTR\0") so the runtime can
locate its module sections; its presence with no COR header identifies the binary
as Native AOT compiled .NET.

**Parameters:**

- `HeaderOffset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): File offset of the RTR signature.
- `MajorVersion` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)): ReadyToRun format major version — the ILC toolchain format version.
- `MinorVersion` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)): ReadyToRun format minor version.
- `Flags` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): Raw header flags.
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of module section entries following the header.
- `EntrySize` ([Byte](https://learn.microsoft.com/dotnet/api/system.byte)): Size in bytes of each module section entry.
- `RuntimeVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Heuristically detected runtime version (e.g. "10.0.8"), or null when not found.
Recovered from a version string the runtime pack embeds near a well-known error
message; absence is normal for stripped or unusually linked binaries.

```csharp
public NativeAotInfo(int HeaderOffset, ushort MajorVersion, ushort MinorVersion, uint Flags, int SectionCount, byte EntrySize, string? RuntimeVersion)
```

## Properties

### EntrySize

Size in bytes of each module section entry.

**Returns:** [Byte](https://learn.microsoft.com/dotnet/api/system.byte)

```csharp
public byte EntrySize { get; init; }
```

### Flags

Raw header flags.

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint Flags { get; init; }
```

### HeaderOffset

File offset of the RTR signature.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int HeaderOffset { get; init; }
```

### MajorVersion

ReadyToRun format major version — the ILC toolchain format version.

**Returns:** [UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)

```csharp
public ushort MajorVersion { get; init; }
```

### MinorVersion

ReadyToRun format minor version.

**Returns:** [UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)

```csharp
public ushort MinorVersion { get; init; }
```

### RuntimeVersion

Heuristically detected runtime version (e.g. "10.0.8"), or null when not found.
Recovered from a version string the runtime pack embeds near a well-known error
message; absence is normal for stripped or unusually linked binaries.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? RuntimeVersion { get; init; }
```

### SectionCount

Number of module section entries following the header.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SectionCount { get; init; }
```

