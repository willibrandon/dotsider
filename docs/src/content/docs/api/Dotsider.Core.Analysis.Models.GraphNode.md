---
title: "GraphNode"
description: "A node in the assembly dependency graph."
slug: api/dotsider.core.analysis.models.graphnode
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

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `Version` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `PublicKeyToken` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `IsRoot` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): 
- `X` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 
- `Y` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 

```csharp
public GraphNode(string Name, string? Version, string? PublicKeyToken, bool IsRoot, double X, double Y)
```

## Properties

### IsRoot

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsRoot { get; init; }
```

### Name

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### PublicKeyToken

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PublicKeyToken { get; init; }
```

### Version

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Version { get; init; }
```

### X

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double X { get; init; }
```

### Y

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double Y { get; init; }
```

