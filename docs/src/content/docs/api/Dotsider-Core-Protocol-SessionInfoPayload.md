---
title: "SessionInfoPayload"
description: "Assembly and view state returned for one live session. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.sessioninfopayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Assembly and view state returned for one live session.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record SessionInfoPayload : IEquatable<SessionInfoPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SessionInfoPayload**

## Implements

- [IEquatable\<SessionInfoPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SessionInfoPayload(JsonElement?, JsonElement?)

Assembly and view state returned for one live session.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Assembly` ([Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `View` ([Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public SessionInfoPayload(JsonElement? Assembly, JsonElement? View)
```

## Properties

### Assembly

**Returns:** [Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public JsonElement? Assembly { get; init; }
```

### View

**Returns:** [Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public JsonElement? View { get; init; }
```

## Methods

### Deconstruct(out JsonElement?, out JsonElement?)

**Parameters:**

- `Assembly` ([Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `View` ([Nullable\<JsonElement\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public void Deconstruct(out JsonElement? Assembly, out JsonElement? View)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SessionInfoPayload?)

**Parameters:**

- `other` ([SessionInfoPayload](/api/dotsider.core.protocol.sessioninfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SessionInfoPayload? other)
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

### operator !=(SessionInfoPayload?, SessionInfoPayload?)

**Parameters:**

- `left` ([SessionInfoPayload](/api/dotsider.core.protocol.sessioninfopayload/))
- `right` ([SessionInfoPayload](/api/dotsider.core.protocol.sessioninfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SessionInfoPayload? left, SessionInfoPayload? right)
```

### operator ==(SessionInfoPayload?, SessionInfoPayload?)

**Parameters:**

- `left` ([SessionInfoPayload](/api/dotsider.core.protocol.sessioninfopayload/))
- `right` ([SessionInfoPayload](/api/dotsider.core.protocol.sessioninfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SessionInfoPayload? left, SessionInfoPayload? right)
```
