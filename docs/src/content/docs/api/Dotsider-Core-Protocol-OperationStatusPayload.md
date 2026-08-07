---
title: "OperationStatusPayload"
description: "A queued-operation status. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.operationstatuspayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A queued-operation status.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record OperationStatusPayload : IEquatable<OperationStatusPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **OperationStatusPayload**

## Implements

- [IEquatable\<OperationStatusPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### OperationStatusPayload(string)

A queued-operation status.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Status` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public OperationStatusPayload(string Status)
```

## Properties

### Status

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Status { get; init; }
```

## Methods

### Deconstruct(out string)

**Parameters:**

- `Status` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Status)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(OperationStatusPayload?)

**Parameters:**

- `other` ([OperationStatusPayload](/api/dotsider.core.protocol.operationstatuspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(OperationStatusPayload? other)
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

### operator !=(OperationStatusPayload?, OperationStatusPayload?)

**Parameters:**

- `left` ([OperationStatusPayload](/api/dotsider.core.protocol.operationstatuspayload/))
- `right` ([OperationStatusPayload](/api/dotsider.core.protocol.operationstatuspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(OperationStatusPayload? left, OperationStatusPayload? right)
```

### operator ==(OperationStatusPayload?, OperationStatusPayload?)

**Parameters:**

- `left` ([OperationStatusPayload](/api/dotsider.core.protocol.operationstatuspayload/))
- `right` ([OperationStatusPayload](/api/dotsider.core.protocol.operationstatuspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(OperationStatusPayload? left, OperationStatusPayload? right)
```
