---
title: "NativeSymbolMethodPayload"
description: "A native-symbol method identity. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.nativesymbolmethodpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A native-symbol method identity.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeSymbolMethodPayload : IEquatable<NativeSymbolMethodPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeSymbolMethodPayload**

## Implements

- [IEquatable\<NativeSymbolMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeSymbolMethodPayload(string, string)

A native-symbol method identity.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Address` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public NativeSymbolMethodPayload(string Name, string Address)
```

## Properties

### Address

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Address { get; init; }
```

### Name

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

## Methods

### Deconstruct(out string, out string)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Address` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Name, out string Address)
```

### Equals(NativeSymbolMethodPayload?)

**Parameters:**

- `other` ([NativeSymbolMethodPayload](/api/dotsider.core.protocol.nativesymbolmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeSymbolMethodPayload? other)
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

### operator !=(NativeSymbolMethodPayload?, NativeSymbolMethodPayload?)

**Parameters:**

- `left` ([NativeSymbolMethodPayload](/api/dotsider.core.protocol.nativesymbolmethodpayload/))
- `right` ([NativeSymbolMethodPayload](/api/dotsider.core.protocol.nativesymbolmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeSymbolMethodPayload? left, NativeSymbolMethodPayload? right)
```

### operator ==(NativeSymbolMethodPayload?, NativeSymbolMethodPayload?)

**Parameters:**

- `left` ([NativeSymbolMethodPayload](/api/dotsider.core.protocol.nativesymbolmethodpayload/))
- `right` ([NativeSymbolMethodPayload](/api/dotsider.core.protocol.nativesymbolmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeSymbolMethodPayload? left, NativeSymbolMethodPayload? right)
```
