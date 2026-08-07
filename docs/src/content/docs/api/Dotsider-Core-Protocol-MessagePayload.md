---
title: "MessagePayload"
description: "A status or queued-operation message. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.messagepayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A status or queued-operation message.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MessagePayload : IEquatable<MessagePayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MessagePayload**

## Implements

- [IEquatable\<MessagePayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MessagePayload(string)

A status or queued-operation message.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Message` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public MessagePayload(string Message)
```

## Properties

### Message

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Message { get; init; }
```

## Methods

### Deconstruct(out string)

**Parameters:**

- `Message` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string Message)
```

### Equals(MessagePayload?)

**Parameters:**

- `other` ([MessagePayload](/api/dotsider.core.protocol.messagepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MessagePayload? other)
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

### operator !=(MessagePayload?, MessagePayload?)

**Parameters:**

- `left` ([MessagePayload](/api/dotsider.core.protocol.messagepayload/))
- `right` ([MessagePayload](/api/dotsider.core.protocol.messagepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MessagePayload? left, MessagePayload? right)
```

### operator ==(MessagePayload?, MessagePayload?)

**Parameters:**

- `left` ([MessagePayload](/api/dotsider.core.protocol.messagepayload/))
- `right` ([MessagePayload](/api/dotsider.core.protocol.messagepayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MessagePayload? left, MessagePayload? right)
```
