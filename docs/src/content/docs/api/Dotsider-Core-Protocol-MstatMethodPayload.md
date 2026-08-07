---
title: "MstatMethodPayload"
description: "A method identity extracted from an mstat entry. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.mstatmethodpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A method identity extracted from an mstat entry.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatMethodPayload : IEquatable<MstatMethodPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatMethodPayload**

## Implements

- [IEquatable\<MstatMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatMethodPayload(string, string, string, string, string?)

A method identity extracted from an mstat entry.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public MstatMethodPayload(string AssemblyName, string Namespace, string DeclaringType, string Name, string? Signature)
```

## Properties

### AssemblyName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### DeclaringType

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DeclaringType { get; init; }
```

### Name

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Namespace

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Namespace { get; init; }
```

### Signature

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Signature { get; init; }
```

## Methods

### Deconstruct(out string, out string, out string, out string, out string?)

**Parameters:**

- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string AssemblyName, out string Namespace, out string DeclaringType, out string Name, out string? Signature)
```

### Equals(MstatMethodPayload?)

**Parameters:**

- `other` ([MstatMethodPayload](/api/dotsider.core.protocol.mstatmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatMethodPayload? other)
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

### operator !=(MstatMethodPayload?, MstatMethodPayload?)

**Parameters:**

- `left` ([MstatMethodPayload](/api/dotsider.core.protocol.mstatmethodpayload/))
- `right` ([MstatMethodPayload](/api/dotsider.core.protocol.mstatmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatMethodPayload? left, MstatMethodPayload? right)
```

### operator ==(MstatMethodPayload?, MstatMethodPayload?)

**Parameters:**

- `left` ([MstatMethodPayload](/api/dotsider.core.protocol.mstatmethodpayload/))
- `right` ([MstatMethodPayload](/api/dotsider.core.protocol.mstatmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatMethodPayload? left, MstatMethodPayload? right)
```
