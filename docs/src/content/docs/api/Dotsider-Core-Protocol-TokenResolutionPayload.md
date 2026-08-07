---
title: "TokenResolutionPayload"
description: "A metadata token resolution. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.tokenresolutionpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A metadata token resolution.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record TokenResolutionPayload : IEquatable<TokenResolutionPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **TokenResolutionPayload**

## Implements

- [IEquatable\<TokenResolutionPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### TokenResolutionPayload(int, string)

A metadata token resolution.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Resolved` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public TokenResolutionPayload(int Token, string Resolved)
```

## Properties

### Resolved

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Resolved { get; init; }
```

### Token

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

## Methods

### Deconstruct(out int, out string)

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Resolved` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out int Token, out string Resolved)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(TokenResolutionPayload?)

**Parameters:**

- `other` ([TokenResolutionPayload](/api/dotsider.core.protocol.tokenresolutionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(TokenResolutionPayload? other)
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

### operator !=(TokenResolutionPayload?, TokenResolutionPayload?)

**Parameters:**

- `left` ([TokenResolutionPayload](/api/dotsider.core.protocol.tokenresolutionpayload/))
- `right` ([TokenResolutionPayload](/api/dotsider.core.protocol.tokenresolutionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(TokenResolutionPayload? left, TokenResolutionPayload? right)
```

### operator ==(TokenResolutionPayload?, TokenResolutionPayload?)

**Parameters:**

- `left` ([TokenResolutionPayload](/api/dotsider.core.protocol.tokenresolutionpayload/))
- `right` ([TokenResolutionPayload](/api/dotsider.core.protocol.tokenresolutionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(TokenResolutionPayload? left, TokenResolutionPayload? right)
```
