---
title: "LoadConfigInfo"
description: "Parsed PE load configuration directory. Pointer-width fields are widened to UInt64 so a single record covers PE32 and PE32+ images. Fields beyond the directory's declared size are zero — real-world load configs are truncated at many historical lengths."
slug: api/dotsider.core.analysis.models.loadconfiginfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Parsed PE load configuration directory. Pointer-width fields are widened to
[UInt64](https://learn.microsoft.com/dotnet/api/system.uint64) so a single record covers PE32 and PE32+ images. Fields
beyond the directory's declared size are zero — real-world load configs are
truncated at many historical lengths.

```csharp
public sealed record LoadConfigInfo : IEquatable<LoadConfigInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **LoadConfigInfo**

## Implements

- [IEquatable\<LoadConfigInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### LoadConfigInfo(uint, uint, ushort, ushort, ushort, ulong, ulong, ulong, ulong, uint, string)

Parsed PE load configuration directory. Pointer-width fields are widened to
[UInt64](https://learn.microsoft.com/dotnet/api/system.uint64) so a single record covers PE32 and PE32+ images. Fields
beyond the directory's declared size are zero — real-world load configs are
truncated at many historical lengths.

**Parameters:**

- `Size` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): The declared size of the load configuration directory.
- `TimeDateStamp` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): The directory timestamp.
- `MajorVersion` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)): Major version number.
- `MinorVersion` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)): Minor version number.
- `DependentLoadFlags` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)): Default load flags applied when resolving DLL dependencies.
- `SecurityCookie` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The VA of the /GS security cookie, or 0 when absent.
- `SehHandlerCount` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): Number of registered structured exception handlers (PE32 /SAFESEH).
- `GuardCfCheckFunctionPointer` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The VA of the Control Flow Guard check-function pointer, or 0.
- `GuardCfFunctionCount` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): Number of entries in the Control Flow Guard function table.
- `GuardFlags` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): Raw Control Flow Guard flags.
- `GuardFlagsDescription` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Decoded GuardFlags summary, or "(none)".

```csharp
public LoadConfigInfo(uint Size, uint TimeDateStamp, ushort MajorVersion, ushort MinorVersion, ushort DependentLoadFlags, ulong SecurityCookie, ulong SehHandlerCount, ulong GuardCfCheckFunctionPointer, ulong GuardCfFunctionCount, uint GuardFlags, string GuardFlagsDescription)
```

## Properties

### DependentLoadFlags

Default load flags applied when resolving DLL dependencies.

**Returns:** [UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)

```csharp
public ushort DependentLoadFlags { get; init; }
```

### GuardCfCheckFunctionPointer

The VA of the Control Flow Guard check-function pointer, or 0.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong GuardCfCheckFunctionPointer { get; init; }
```

### GuardCfFunctionCount

Number of entries in the Control Flow Guard function table.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong GuardCfFunctionCount { get; init; }
```

### GuardFlags

Raw Control Flow Guard flags.

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint GuardFlags { get; init; }
```

### GuardFlagsDescription

Decoded GuardFlags summary, or "(none)".

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string GuardFlagsDescription { get; init; }
```

### MajorVersion

Major version number.

**Returns:** [UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)

```csharp
public ushort MajorVersion { get; init; }
```

### MinorVersion

Minor version number.

**Returns:** [UInt16](https://learn.microsoft.com/dotnet/api/system.uint16)

```csharp
public ushort MinorVersion { get; init; }
```

### SecurityCookie

The VA of the /GS security cookie, or 0 when absent.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong SecurityCookie { get; init; }
```

### SehHandlerCount

Number of registered structured exception handlers (PE32 /SAFESEH).

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong SehHandlerCount { get; init; }
```

### Size

The declared size of the load configuration directory.

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint Size { get; init; }
```

### TimeDateStamp

The directory timestamp.

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint TimeDateStamp { get; init; }
```

## Methods

### Deconstruct(out uint, out uint, out ushort, out ushort, out ushort, out ulong, out ulong, out ulong, out ulong, out uint, out string)

**Parameters:**

- `Size` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32))
- `TimeDateStamp` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32))
- `MajorVersion` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16))
- `MinorVersion` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16))
- `DependentLoadFlags` ([UInt16](https://learn.microsoft.com/dotnet/api/system.uint16))
- `SecurityCookie` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `SehHandlerCount` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `GuardCfCheckFunctionPointer` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `GuardCfFunctionCount` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `GuardFlags` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32))
- `GuardFlagsDescription` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out uint Size, out uint TimeDateStamp, out ushort MajorVersion, out ushort MinorVersion, out ushort DependentLoadFlags, out ulong SecurityCookie, out ulong SehHandlerCount, out ulong GuardCfCheckFunctionPointer, out ulong GuardCfFunctionCount, out uint GuardFlags, out string GuardFlagsDescription)
```

### Equals(LoadConfigInfo?)

**Parameters:**

- `other` ([LoadConfigInfo](/api/dotsider.core.analysis.models.loadconfiginfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(LoadConfigInfo? other)
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

### operator !=(LoadConfigInfo?, LoadConfigInfo?)

**Parameters:**

- `left` ([LoadConfigInfo](/api/dotsider.core.analysis.models.loadconfiginfo/))
- `right` ([LoadConfigInfo](/api/dotsider.core.analysis.models.loadconfiginfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(LoadConfigInfo? left, LoadConfigInfo? right)
```

### operator ==(LoadConfigInfo?, LoadConfigInfo?)

**Parameters:**

- `left` ([LoadConfigInfo](/api/dotsider.core.analysis.models.loadconfiginfo/))
- `right` ([LoadConfigInfo](/api/dotsider.core.analysis.models.loadconfiginfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(LoadConfigInfo? left, LoadConfigInfo? right)
```
