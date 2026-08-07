---
title: "ReadyToRunAmbiguityPayload"
description: "An ambiguous ReadyToRun method query. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.readytorunambiguitypayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

An ambiguous ReadyToRun method query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record ReadyToRunAmbiguityPayload : IEquatable<ReadyToRunAmbiguityPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunAmbiguityPayload**

## Implements

- [IEquatable\<ReadyToRunAmbiguityPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ReadyToRunAmbiguityPayload(string, string, IReadOnlyList\<CorrelationCandidate\>)

An ambiguous ReadyToRun method query.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Error` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Candidates` ([IReadOnlyList\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public ReadyToRunAmbiguityPayload(string Error, string Target, IReadOnlyList<CorrelationCandidate> Candidates)
```

## Properties

### Candidates

**Returns:** [IReadOnlyList\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<CorrelationCandidate> Candidates { get; init; }
```

### Error

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Error { get; init; }
```

### Target

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Target { get; init; }
```

## Methods

### Deconstruct(out string, out string, out IReadOnlyList\<CorrelationCandidate\>)

**Parameters:**

- `Error` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Candidates` ([IReadOnlyList\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string Error, out string Target, out IReadOnlyList<CorrelationCandidate> Candidates)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(ReadyToRunAmbiguityPayload?)

**Parameters:**

- `other` ([ReadyToRunAmbiguityPayload](/api/dotsider.core.protocol.readytorunambiguitypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ReadyToRunAmbiguityPayload? other)
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

### operator !=(ReadyToRunAmbiguityPayload?, ReadyToRunAmbiguityPayload?)

**Parameters:**

- `left` ([ReadyToRunAmbiguityPayload](/api/dotsider.core.protocol.readytorunambiguitypayload/))
- `right` ([ReadyToRunAmbiguityPayload](/api/dotsider.core.protocol.readytorunambiguitypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ReadyToRunAmbiguityPayload? left, ReadyToRunAmbiguityPayload? right)
```

### operator ==(ReadyToRunAmbiguityPayload?, ReadyToRunAmbiguityPayload?)

**Parameters:**

- `left` ([ReadyToRunAmbiguityPayload](/api/dotsider.core.protocol.readytorunambiguitypayload/))
- `right` ([ReadyToRunAmbiguityPayload](/api/dotsider.core.protocol.readytorunambiguitypayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ReadyToRunAmbiguityPayload? left, ReadyToRunAmbiguityPayload? right)
```
