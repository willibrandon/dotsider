---
title: "ByteRangePayload"
description: "Bytes read from a binary at a requested offset. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.byterangepayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Bytes read from a binary at a requested offset.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record ByteRangePayload : IEquatable<ByteRangePayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ByteRangePayload**

## Implements

- [IEquatable\<ByteRangePayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ByteRangePayload(int, int, string, string)

Bytes read from a binary at a requested offset.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Offset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Length` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Hex` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Base64` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public ByteRangePayload(int Offset, int Length, string Hex, string Base64)
```

## Properties

### Base64

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Base64 { get; init; }
```

### Hex

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Hex { get; init; }
```

### Length

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Length { get; init; }
```

### Offset

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Offset { get; init; }
```

## Methods

### Deconstruct(out int, out int, out string, out string)

**Parameters:**

- `Offset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Length` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Hex` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Base64` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out int Offset, out int Length, out string Hex, out string Base64)
```

### Equals(ByteRangePayload?)

**Parameters:**

- `other` ([ByteRangePayload](/api/dotsider.core.protocol.byterangepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ByteRangePayload? other)
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

### operator !=(ByteRangePayload?, ByteRangePayload?)

**Parameters:**

- `left` ([ByteRangePayload](/api/dotsider.core.protocol.byterangepayload/))
- `right` ([ByteRangePayload](/api/dotsider.core.protocol.byterangepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ByteRangePayload? left, ByteRangePayload? right)
```

### operator ==(ByteRangePayload?, ByteRangePayload?)

**Parameters:**

- `left` ([ByteRangePayload](/api/dotsider.core.protocol.byterangepayload/))
- `right` ([ByteRangePayload](/api/dotsider.core.protocol.byterangepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ByteRangePayload? left, ByteRangePayload? right)
```
