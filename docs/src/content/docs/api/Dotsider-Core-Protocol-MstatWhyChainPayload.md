---
title: "MstatWhyChainPayload"
description: "One dependency chain explaining a Native AOT size contributor. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.mstatwhychainpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

One dependency chain explaining a Native AOT size contributor.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatWhyChainPayload : IEquatable<MstatWhyChainPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatWhyChainPayload**

## Implements

- [IEquatable\<MstatWhyChainPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatWhyChainPayload(string, bool, IReadOnlyList\<DgmlPathStep\>)

One dependency chain explaining a Native AOT size contributor.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `NodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Found` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `Steps` ([IReadOnlyList\<DgmlPathStep\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public MstatWhyChainPayload(string NodeName, bool Found, IReadOnlyList<DgmlPathStep> Steps)
```

## Properties

### Found

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Found { get; init; }
```

### NodeName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string NodeName { get; init; }
```

### Steps

**Returns:** [IReadOnlyList\<DgmlPathStep\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DgmlPathStep> Steps { get; init; }
```

## Methods

### Deconstruct(out string, out bool, out IReadOnlyList\<DgmlPathStep\>)

**Parameters:**

- `NodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Found` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `Steps` ([IReadOnlyList\<DgmlPathStep\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string NodeName, out bool Found, out IReadOnlyList<DgmlPathStep> Steps)
```

### Equals(MstatWhyChainPayload?)

**Parameters:**

- `other` ([MstatWhyChainPayload](/api/dotsider.core.protocol.mstatwhychainpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatWhyChainPayload? other)
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

### operator !=(MstatWhyChainPayload?, MstatWhyChainPayload?)

**Parameters:**

- `left` ([MstatWhyChainPayload](/api/dotsider.core.protocol.mstatwhychainpayload/))
- `right` ([MstatWhyChainPayload](/api/dotsider.core.protocol.mstatwhychainpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatWhyChainPayload? left, MstatWhyChainPayload? right)
```

### operator ==(MstatWhyChainPayload?, MstatWhyChainPayload?)

**Parameters:**

- `left` ([MstatWhyChainPayload](/api/dotsider.core.protocol.mstatwhychainpayload/))
- `right` ([MstatWhyChainPayload](/api/dotsider.core.protocol.mstatwhychainpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatWhyChainPayload? left, MstatWhyChainPayload? right)
```
