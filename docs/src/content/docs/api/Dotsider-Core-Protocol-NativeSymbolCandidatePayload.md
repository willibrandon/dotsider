---
title: "NativeSymbolCandidatePayload"
description: "One candidate for an ambiguous native-symbol query. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.nativesymbolcandidatepayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

One candidate for an ambiguous native-symbol query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeSymbolCandidatePayload : IEquatable<NativeSymbolCandidatePayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeSymbolCandidatePayload**

## Implements

- [IEquatable\<NativeSymbolCandidatePayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeSymbolCandidatePayload(string, string)

One candidate for an ambiguous native-symbol query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Address` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public NativeSymbolCandidatePayload(string Address, string Name)
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

- `Address` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Address, out string Name)
```

### Equals(NativeSymbolCandidatePayload?)

**Parameters:**

- `other` ([NativeSymbolCandidatePayload](/api/dotsider.core.protocol.nativesymbolcandidatepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeSymbolCandidatePayload? other)
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

### operator !=(NativeSymbolCandidatePayload?, NativeSymbolCandidatePayload?)

**Parameters:**

- `left` ([NativeSymbolCandidatePayload](/api/dotsider.core.protocol.nativesymbolcandidatepayload/))
- `right` ([NativeSymbolCandidatePayload](/api/dotsider.core.protocol.nativesymbolcandidatepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeSymbolCandidatePayload? left, NativeSymbolCandidatePayload? right)
```

### operator ==(NativeSymbolCandidatePayload?, NativeSymbolCandidatePayload?)

**Parameters:**

- `left` ([NativeSymbolCandidatePayload](/api/dotsider.core.protocol.nativesymbolcandidatepayload/))
- `right` ([NativeSymbolCandidatePayload](/api/dotsider.core.protocol.nativesymbolcandidatepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeSymbolCandidatePayload? left, NativeSymbolCandidatePayload? right)
```
