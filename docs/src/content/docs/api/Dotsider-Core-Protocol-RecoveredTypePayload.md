---
title: "RecoveredTypePayload"
description: "A recovered Native AOT type row. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.recoveredtypepayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A recovered Native AOT type row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record RecoveredTypePayload : IEquatable<RecoveredTypePayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **RecoveredTypePayload**

## Implements

- [IEquatable\<RecoveredTypePayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### RecoveredTypePayload(string, string?, string, int)

A recovered Native AOT type row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Source` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FullName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MethodCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public RecoveredTypePayload(string Source, string? AssemblyName, string FullName, int MethodCount)
```

## Properties

### AssemblyName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyName { get; init; }
```

### FullName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullName { get; init; }
```

### MethodCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodCount { get; init; }
```

### Source

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Source { get; init; }
```

## Methods

### Deconstruct(out string, out string?, out string, out int)

**Parameters:**

- `Source` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FullName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MethodCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out string Source, out string? AssemblyName, out string FullName, out int MethodCount)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(RecoveredTypePayload?)

**Parameters:**

- `other` ([RecoveredTypePayload](/api/dotsider.core.protocol.recoveredtypepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(RecoveredTypePayload? other)
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

### operator !=(RecoveredTypePayload?, RecoveredTypePayload?)

**Parameters:**

- `left` ([RecoveredTypePayload](/api/dotsider.core.protocol.recoveredtypepayload/))
- `right` ([RecoveredTypePayload](/api/dotsider.core.protocol.recoveredtypepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(RecoveredTypePayload? left, RecoveredTypePayload? right)
```

### operator ==(RecoveredTypePayload?, RecoveredTypePayload?)

**Parameters:**

- `left` ([RecoveredTypePayload](/api/dotsider.core.protocol.recoveredtypepayload/))
- `right` ([RecoveredTypePayload](/api/dotsider.core.protocol.recoveredtypepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(RecoveredTypePayload? left, RecoveredTypePayload? right)
```
