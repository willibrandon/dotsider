---
title: "IlSearchResultPayload"
description: "Opcode matches within one method. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.ilsearchresultpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Opcode matches within one method.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record IlSearchResultPayload : IEquatable<IlSearchResultPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **IlSearchResultPayload**

## Implements

- [IEquatable\<IlSearchResultPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### IlSearchResultPayload(string, IReadOnlyList\<IlInstruction\>)

Opcode matches within one method.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Method` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Matches` ([IReadOnlyList\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public IlSearchResultPayload(string Method, IReadOnlyList<IlInstruction> Matches)
```

## Properties

### Matches

**Returns:** [IReadOnlyList\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<IlInstruction> Matches { get; init; }
```

### Method

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Method { get; init; }
```

## Methods

### Deconstruct(out string, out IReadOnlyList\<IlInstruction\>)

**Parameters:**

- `Method` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Matches` ([IReadOnlyList\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string Method, out IReadOnlyList<IlInstruction> Matches)
```

### Equals(IlSearchResultPayload?)

**Parameters:**

- `other` ([IlSearchResultPayload](/api/dotsider.core.protocol.ilsearchresultpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlSearchResultPayload? other)
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

### operator !=(IlSearchResultPayload?, IlSearchResultPayload?)

**Parameters:**

- `left` ([IlSearchResultPayload](/api/dotsider.core.protocol.ilsearchresultpayload/))
- `right` ([IlSearchResultPayload](/api/dotsider.core.protocol.ilsearchresultpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlSearchResultPayload? left, IlSearchResultPayload? right)
```

### operator ==(IlSearchResultPayload?, IlSearchResultPayload?)

**Parameters:**

- `left` ([IlSearchResultPayload](/api/dotsider.core.protocol.ilsearchresultpayload/))
- `right` ([IlSearchResultPayload](/api/dotsider.core.protocol.ilsearchresultpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlSearchResultPayload? left, IlSearchResultPayload? right)
```
