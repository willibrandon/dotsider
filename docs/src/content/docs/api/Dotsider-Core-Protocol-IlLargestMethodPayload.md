---
title: "IlLargestMethodPayload"
description: "A large IL method. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.illargestmethodpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A large IL method.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record IlLargestMethodPayload : IEquatable<IlLargestMethodPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **IlLargestMethodPayload**

## Implements

- [IEquatable\<IlLargestMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### IlLargestMethodPayload(MethodDefInfo, int)

A large IL method.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/))
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public IlLargestMethodPayload(MethodDefInfo Method, int Size)
```

## Properties

### Method

**Returns:** [MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)

```csharp
public MethodDefInfo Method { get; init; }
```

### Size

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

## Methods

### Deconstruct(out MethodDefInfo, out int)

**Parameters:**

- `Method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/))
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out MethodDefInfo Method, out int Size)
```

### Equals(IlLargestMethodPayload?)

**Parameters:**

- `other` ([IlLargestMethodPayload](/api/dotsider.core.protocol.illargestmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlLargestMethodPayload? other)
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

### operator !=(IlLargestMethodPayload?, IlLargestMethodPayload?)

**Parameters:**

- `left` ([IlLargestMethodPayload](/api/dotsider.core.protocol.illargestmethodpayload/))
- `right` ([IlLargestMethodPayload](/api/dotsider.core.protocol.illargestmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlLargestMethodPayload? left, IlLargestMethodPayload? right)
```

### operator ==(IlLargestMethodPayload?, IlLargestMethodPayload?)

**Parameters:**

- `left` ([IlLargestMethodPayload](/api/dotsider.core.protocol.illargestmethodpayload/))
- `right` ([IlLargestMethodPayload](/api/dotsider.core.protocol.illargestmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlLargestMethodPayload? left, IlLargestMethodPayload? right)
```
