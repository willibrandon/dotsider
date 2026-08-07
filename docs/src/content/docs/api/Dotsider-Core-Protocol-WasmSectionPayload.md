---
title: "WasmSectionPayload"
description: "A WebAssembly section row. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.wasmsectionpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A WebAssembly section row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WasmSectionPayload : IEquatable<WasmSectionPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmSectionPayload**

## Implements

- [IEquatable\<WasmSectionPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmSectionPayload(byte, string, long, long)

A WebAssembly section row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Id` ([Byte](https://learn.microsoft.com/dotnet/api/system.byte))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FileOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public WasmSectionPayload(byte Id, string Name, long FileOffset, long Size)
```

## Properties

### FileOffset

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long FileOffset { get; init; }
```

### Id

**Returns:** [Byte](https://learn.microsoft.com/dotnet/api/system.byte)

```csharp
public byte Id { get; init; }
```

### Name

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Size

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

## Methods

### Deconstruct(out byte, out string, out long, out long)

**Parameters:**

- `Id` ([Byte](https://learn.microsoft.com/dotnet/api/system.byte))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FileOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public void Deconstruct(out byte Id, out string Name, out long FileOffset, out long Size)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmSectionPayload?)

**Parameters:**

- `other` ([WasmSectionPayload](/api/dotsider.core.protocol.wasmsectionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmSectionPayload? other)
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

### operator !=(WasmSectionPayload?, WasmSectionPayload?)

**Parameters:**

- `left` ([WasmSectionPayload](/api/dotsider.core.protocol.wasmsectionpayload/))
- `right` ([WasmSectionPayload](/api/dotsider.core.protocol.wasmsectionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmSectionPayload? left, WasmSectionPayload? right)
```

### operator ==(WasmSectionPayload?, WasmSectionPayload?)

**Parameters:**

- `left` ([WasmSectionPayload](/api/dotsider.core.protocol.wasmsectionpayload/))
- `right` ([WasmSectionPayload](/api/dotsider.core.protocol.wasmsectionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmSectionPayload? left, WasmSectionPayload? right)
```
