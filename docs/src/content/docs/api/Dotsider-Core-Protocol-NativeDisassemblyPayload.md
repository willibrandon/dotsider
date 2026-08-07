---
title: "NativeDisassemblyPayload"
description: "Decoded native instructions for one symbol. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.nativedisassemblypayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Decoded native instructions for one symbol.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record NativeDisassemblyPayload : IEquatable<NativeDisassemblyPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeDisassemblyPayload**

## Implements

- [IEquatable\<NativeDisassemblyPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeDisassemblyPayload(string, string, IReadOnlyList\<NativeInstruction\>)

Decoded native instructions for one symbol.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Symbol` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Architecture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Instructions` ([IReadOnlyList\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public NativeDisassemblyPayload(string Symbol, string Architecture, IReadOnlyList<NativeInstruction> Instructions)
```

## Properties

### Architecture

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Architecture { get; init; }
```

### Instructions

**Returns:** [IReadOnlyList\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NativeInstruction> Instructions { get; init; }
```

### Symbol

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Symbol { get; init; }
```

## Methods

### Deconstruct(out string, out string, out IReadOnlyList\<NativeInstruction\>)

**Parameters:**

- `Symbol` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Architecture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Instructions` ([IReadOnlyList\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string Symbol, out string Architecture, out IReadOnlyList<NativeInstruction> Instructions)
```

### Equals(NativeDisassemblyPayload?)

**Parameters:**

- `other` ([NativeDisassemblyPayload](/api/dotsider.core.protocol.nativedisassemblypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeDisassemblyPayload? other)
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

### operator !=(NativeDisassemblyPayload?, NativeDisassemblyPayload?)

**Parameters:**

- `left` ([NativeDisassemblyPayload](/api/dotsider.core.protocol.nativedisassemblypayload/))
- `right` ([NativeDisassemblyPayload](/api/dotsider.core.protocol.nativedisassemblypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeDisassemblyPayload? left, NativeDisassemblyPayload? right)
```

### operator ==(NativeDisassemblyPayload?, NativeDisassemblyPayload?)

**Parameters:**

- `left` ([NativeDisassemblyPayload](/api/dotsider.core.protocol.nativedisassemblypayload/))
- `right` ([NativeDisassemblyPayload](/api/dotsider.core.protocol.nativedisassemblypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeDisassemblyPayload? left, NativeDisassemblyPayload? right)
```
