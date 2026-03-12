---
title: "GraphNode"
description: "A node in the assembly dependency graph."
slug: api/dotsider.core.analysis.models.graphnode
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A node in the assembly dependency graph.

```csharp
public sealed record GraphNode : IEquatable<GraphNode>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **GraphNode**

## Implements

- [IEquatable\<GraphNode\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### GraphNode(string, string?, string?, bool, double, double)

A node in the assembly dependency graph.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Assembly name.
- `Version` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Assembly version string, or null if unavailable.
- `PublicKeyToken` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Public key token hex string, or null.
- `IsRoot` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether this is the root (analyzed) assembly.
- `X` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): X coordinate for graph layout rendering.
- `Y` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): Y coordinate for graph layout rendering.

```csharp
public GraphNode(string Name, string? Version, string? PublicKeyToken, bool IsRoot, double X, double Y)
```

## Properties

### IsRoot

Whether this is the root (analyzed) assembly.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsRoot { get; init; }
```

### Name

Assembly name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### PublicKeyToken

Public key token hex string, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PublicKeyToken { get; init; }
```

### Version

Assembly version string, or null if unavailable.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Version { get; init; }
```

### X

X coordinate for graph layout rendering.

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double X { get; init; }
```

### Y

Y coordinate for graph layout rendering.

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double Y { get; init; }
```

