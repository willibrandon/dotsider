---
title: "GraphEdge"
description: "An edge connecting two nodes in the dependency graph."
slug: api/dotsider.core.analysis.models.graphedge
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

An edge connecting two nodes in the dependency graph.

```csharp
public sealed record GraphEdge : IEquatable<GraphEdge>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **GraphEdge**

## Implements

- [IEquatable\<GraphEdge\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### GraphEdge(string, string, int)

An edge connecting two nodes in the dependency graph.

**Parameters:**

- `SourceName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `TargetName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `TypeRefCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 

```csharp
public GraphEdge(string SourceName, string TargetName, int TypeRefCount)
```

## Properties

### SourceName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string SourceName { get; init; }
```

### TargetName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string TargetName { get; init; }
```

### TypeRefCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypeRefCount { get; init; }
```

