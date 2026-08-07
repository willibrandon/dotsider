---
title: "RecoveredMethodPayload"
description: "A recovered Native AOT method row. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.recoveredmethodpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A recovered Native AOT method row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record RecoveredMethodPayload : IEquatable<RecoveredMethodPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **RecoveredMethodPayload**

## Implements

- [IEquatable\<RecoveredMethodPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### RecoveredMethodPayload(string, string?, string, string, int)

A recovered Native AOT method row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Source` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MethodIndex` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public RecoveredMethodPayload(string Source, string? AssemblyName, string DeclaringType, string Name, int MethodIndex)
```

## Properties

### AssemblyName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyName { get; init; }
```

### DeclaringType

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DeclaringType { get; init; }
```

### MethodIndex

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodIndex { get; init; }
```

### Name

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Source

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Source { get; init; }
```

## Methods

### Deconstruct(out string, out string?, out string, out string, out int)

**Parameters:**

- `Source` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MethodIndex` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out string Source, out string? AssemblyName, out string DeclaringType, out string Name, out int MethodIndex)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(RecoveredMethodPayload?)

**Parameters:**

- `other` ([RecoveredMethodPayload](/api/dotsider.core.protocol.recoveredmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(RecoveredMethodPayload? other)
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

### operator !=(RecoveredMethodPayload?, RecoveredMethodPayload?)

**Parameters:**

- `left` ([RecoveredMethodPayload](/api/dotsider.core.protocol.recoveredmethodpayload/))
- `right` ([RecoveredMethodPayload](/api/dotsider.core.protocol.recoveredmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(RecoveredMethodPayload? left, RecoveredMethodPayload? right)
```

### operator ==(RecoveredMethodPayload?, RecoveredMethodPayload?)

**Parameters:**

- `left` ([RecoveredMethodPayload](/api/dotsider.core.protocol.recoveredmethodpayload/))
- `right` ([RecoveredMethodPayload](/api/dotsider.core.protocol.recoveredmethodpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(RecoveredMethodPayload? left, RecoveredMethodPayload? right)
```
