---
title: "DiscoveredSessionPayload"
description: "A live dotsider session discovered over its diagnostics socket. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.discoveredsessionpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A live dotsider session discovered over its diagnostics socket.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record DiscoveredSessionPayload : IEquatable<DiscoveredSessionPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DiscoveredSessionPayload**

## Implements

- [IEquatable\<DiscoveredSessionPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### DiscoveredSessionPayload(int, string, JsonElement?)

A live dotsider session discovered over its diagnostics socket.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Pid` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `SocketPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Info` ([Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public DiscoveredSessionPayload(int Pid, string SocketPath, JsonElement? Info)
```

## Properties

### Info

**Returns:** [Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public JsonElement? Info { get; init; }
```

### Pid

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Pid { get; init; }
```

### SocketPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string SocketPath { get; init; }
```

## Methods

### Deconstruct(out int, out string, out JsonElement?)

**Parameters:**

- `Pid` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `SocketPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Info` ([Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public void Deconstruct(out int Pid, out string SocketPath, out JsonElement? Info)
```

### Equals(DiscoveredSessionPayload?)

**Parameters:**

- `other` ([DiscoveredSessionPayload](/api/dotsider.core.protocol.discoveredsessionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(DiscoveredSessionPayload? other)
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

### operator !=(DiscoveredSessionPayload?, DiscoveredSessionPayload?)

**Parameters:**

- `left` ([DiscoveredSessionPayload](/api/dotsider.core.protocol.discoveredsessionpayload/))
- `right` ([DiscoveredSessionPayload](/api/dotsider.core.protocol.discoveredsessionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(DiscoveredSessionPayload? left, DiscoveredSessionPayload? right)
```

### operator ==(DiscoveredSessionPayload?, DiscoveredSessionPayload?)

**Parameters:**

- `left` ([DiscoveredSessionPayload](/api/dotsider.core.protocol.discoveredsessionpayload/))
- `right` ([DiscoveredSessionPayload](/api/dotsider.core.protocol.discoveredsessionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(DiscoveredSessionPayload? left, DiscoveredSessionPayload? right)
```
