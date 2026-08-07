---
title: "NativeSymbolLargestMethodPayload"
description: "A large method reported by native symbols. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.nativesymbollargestmethodpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A large method reported by native symbols.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeSymbolLargestMethodPayload : IEquatable<NativeSymbolLargestMethodPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeSymbolLargestMethodPayload**

## Implements

- [IEquatable\<NativeSymbolLargestMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeSymbolLargestMethodPayload(string, NativeSymbolMethodPayload, long, long?, ulong)

A large method reported by native symbols.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Source` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Method` ([NativeSymbolMethodPayload](/api/dotsider.core.protocol.nativesymbolmethodpayload/))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `FileOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `VirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))

```csharp
public NativeSymbolLargestMethodPayload(string Source, NativeSymbolMethodPayload Method, long Size, long? FileOffset, ulong VirtualAddress)
```

## Properties

### FileOffset

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? FileOffset { get; init; }
```

### Method

**Returns:** [NativeSymbolMethodPayload](/api/dotsider.core.protocol.nativesymbolmethodpayload/)

```csharp
public NativeSymbolMethodPayload Method { get; init; }
```

### Size

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### Source

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Source { get; init; }
```

### VirtualAddress

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong VirtualAddress { get; init; }
```

## Methods

### Deconstruct(out string, out NativeSymbolMethodPayload, out long, out long?, out ulong)

**Parameters:**

- `Source` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Method` ([NativeSymbolMethodPayload](/api/dotsider.core.protocol.nativesymbolmethodpayload/))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `FileOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `VirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))

```csharp
public void Deconstruct(out string Source, out NativeSymbolMethodPayload Method, out long Size, out long? FileOffset, out ulong VirtualAddress)
```

### Equals(NativeSymbolLargestMethodPayload?)

**Parameters:**

- `other` ([NativeSymbolLargestMethodPayload](/api/dotsider.core.protocol.nativesymbollargestmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeSymbolLargestMethodPayload? other)
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

### operator !=(NativeSymbolLargestMethodPayload?, NativeSymbolLargestMethodPayload?)

**Parameters:**

- `left` ([NativeSymbolLargestMethodPayload](/api/dotsider.core.protocol.nativesymbollargestmethodpayload/))
- `right` ([NativeSymbolLargestMethodPayload](/api/dotsider.core.protocol.nativesymbollargestmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeSymbolLargestMethodPayload? left, NativeSymbolLargestMethodPayload? right)
```

### operator ==(NativeSymbolLargestMethodPayload?, NativeSymbolLargestMethodPayload?)

**Parameters:**

- `left` ([NativeSymbolLargestMethodPayload](/api/dotsider.core.protocol.nativesymbollargestmethodpayload/))
- `right` ([NativeSymbolLargestMethodPayload](/api/dotsider.core.protocol.nativesymbollargestmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeSymbolLargestMethodPayload? left, NativeSymbolLargestMethodPayload? right)
```
