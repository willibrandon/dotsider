---
title: "DependencyGraphPayload"
description: "A dependency graph suitable for protocol and MCP responses. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.dependencygraphpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A dependency graph suitable for protocol and MCP responses.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record DependencyGraphPayload : IEquatable<DependencyGraphPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DependencyGraphPayload**

## Implements

- [IEquatable\<DependencyGraphPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### DependencyGraphPayload(IReadOnlyList\<GraphNode\>, IReadOnlyList\<GraphEdge\>)

A dependency graph suitable for protocol and MCP responses.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Nodes` ([IReadOnlyList\<GraphNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Edges` ([IReadOnlyList\<GraphEdge\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public DependencyGraphPayload(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)
```

## Properties

### Edges

**Returns:** [IReadOnlyList\<GraphEdge\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<GraphEdge> Edges { get; init; }
```

### Nodes

**Returns:** [IReadOnlyList\<GraphNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<GraphNode> Nodes { get; init; }
```

## Methods

### Deconstruct(out IReadOnlyList\<GraphNode\>, out IReadOnlyList\<GraphEdge\>)

**Parameters:**

- `Nodes` ([IReadOnlyList\<GraphNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Edges` ([IReadOnlyList\<GraphEdge\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out IReadOnlyList<GraphNode> Nodes, out IReadOnlyList<GraphEdge> Edges)
```

### Equals(DependencyGraphPayload?)

**Parameters:**

- `other` ([DependencyGraphPayload](/api/dotsider.core.protocol.dependencygraphpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(DependencyGraphPayload? other)
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

### operator !=(DependencyGraphPayload?, DependencyGraphPayload?)

**Parameters:**

- `left` ([DependencyGraphPayload](/api/dotsider.core.protocol.dependencygraphpayload/))
- `right` ([DependencyGraphPayload](/api/dotsider.core.protocol.dependencygraphpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(DependencyGraphPayload? left, DependencyGraphPayload? right)
```

### operator ==(DependencyGraphPayload?, DependencyGraphPayload?)

**Parameters:**

- `left` ([DependencyGraphPayload](/api/dotsider.core.protocol.dependencygraphpayload/))
- `right` ([DependencyGraphPayload](/api/dotsider.core.protocol.dependencygraphpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(DependencyGraphPayload? left, DependencyGraphPayload? right)
```
