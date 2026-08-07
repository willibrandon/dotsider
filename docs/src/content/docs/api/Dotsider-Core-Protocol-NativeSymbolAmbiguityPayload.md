---
title: "NativeSymbolAmbiguityPayload"
description: "An ambiguous native-symbol query. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.nativesymbolambiguitypayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

An ambiguous native-symbol query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeSymbolAmbiguityPayload : IEquatable<NativeSymbolAmbiguityPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeSymbolAmbiguityPayload**

## Implements

- [IEquatable\<NativeSymbolAmbiguityPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeSymbolAmbiguityPayload(string, string, IReadOnlyList\<NativeSymbolCandidatePayload\>)

An ambiguous native-symbol query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Error` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Candidates` ([IReadOnlyList\<NativeSymbolCandidatePayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public NativeSymbolAmbiguityPayload(string Error, string Target, IReadOnlyList<NativeSymbolCandidatePayload> Candidates)
```

## Properties

### Candidates

**Returns:** [IReadOnlyList\<NativeSymbolCandidatePayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NativeSymbolCandidatePayload> Candidates { get; init; }
```

### Error

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Error { get; init; }
```

### Target

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Target { get; init; }
```

## Methods

### Deconstruct(out string, out string, out IReadOnlyList\<NativeSymbolCandidatePayload\>)

**Parameters:**

- `Error` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Candidates` ([IReadOnlyList\<NativeSymbolCandidatePayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string Error, out string Target, out IReadOnlyList<NativeSymbolCandidatePayload> Candidates)
```

### Equals(NativeSymbolAmbiguityPayload?)

**Parameters:**

- `other` ([NativeSymbolAmbiguityPayload](/api/dotsider.core.protocol.nativesymbolambiguitypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeSymbolAmbiguityPayload? other)
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

### operator !=(NativeSymbolAmbiguityPayload?, NativeSymbolAmbiguityPayload?)

**Parameters:**

- `left` ([NativeSymbolAmbiguityPayload](/api/dotsider.core.protocol.nativesymbolambiguitypayload/))
- `right` ([NativeSymbolAmbiguityPayload](/api/dotsider.core.protocol.nativesymbolambiguitypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeSymbolAmbiguityPayload? left, NativeSymbolAmbiguityPayload? right)
```

### operator ==(NativeSymbolAmbiguityPayload?, NativeSymbolAmbiguityPayload?)

**Parameters:**

- `left` ([NativeSymbolAmbiguityPayload](/api/dotsider.core.protocol.nativesymbolambiguitypayload/))
- `right` ([NativeSymbolAmbiguityPayload](/api/dotsider.core.protocol.nativesymbolambiguitypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeSymbolAmbiguityPayload? left, NativeSymbolAmbiguityPayload? right)
```
