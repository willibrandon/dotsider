---
title: "NativeAotSectionPayload"
description: "A Native AOT module-section row. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.nativeaotsectionpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A Native AOT module-section row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeAotSectionPayload : IEquatable<NativeAotSectionPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeAotSectionPayload**

## Implements

- [IEquatable\<NativeAotSectionPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeAotSectionPayload(int, string, string, ulong, long, int?, bool)

A Native AOT module-section row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `SectionId` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Address` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `VirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `FileOffset` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `IsMapped` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))

```csharp
public NativeAotSectionPayload(int SectionId, string Name, string Address, ulong VirtualAddress, long Size, int? FileOffset, bool IsMapped)
```

## Properties

### Address

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Address { get; init; }
```

### FileOffset

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? FileOffset { get; init; }
```

### IsMapped

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsMapped { get; init; }
```

### Name

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### SectionId

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SectionId { get; init; }
```

### Size

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### VirtualAddress

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong VirtualAddress { get; init; }
```

## Methods

### Deconstruct(out int, out string, out string, out ulong, out long, out int?, out bool)

**Parameters:**

- `SectionId` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Address` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `VirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `FileOffset` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `IsMapped` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))

```csharp
public void Deconstruct(out int SectionId, out string Name, out string Address, out ulong VirtualAddress, out long Size, out int? FileOffset, out bool IsMapped)
```

### Equals(NativeAotSectionPayload?)

**Parameters:**

- `other` ([NativeAotSectionPayload](/api/dotsider.core.protocol.nativeaotsectionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeAotSectionPayload? other)
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

### operator !=(NativeAotSectionPayload?, NativeAotSectionPayload?)

**Parameters:**

- `left` ([NativeAotSectionPayload](/api/dotsider.core.protocol.nativeaotsectionpayload/))
- `right` ([NativeAotSectionPayload](/api/dotsider.core.protocol.nativeaotsectionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeAotSectionPayload? left, NativeAotSectionPayload? right)
```

### operator ==(NativeAotSectionPayload?, NativeAotSectionPayload?)

**Parameters:**

- `left` ([NativeAotSectionPayload](/api/dotsider.core.protocol.nativeaotsectionpayload/))
- `right` ([NativeAotSectionPayload](/api/dotsider.core.protocol.nativeaotsectionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeAotSectionPayload? left, NativeAotSectionPayload? right)
```
