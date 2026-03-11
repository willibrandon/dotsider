---
title: "TreemapRect"
description: "A positioned rectangle in the treemap layout."
slug: api/dotsider.core.analysis.treemaprect
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

A positioned rectangle in the treemap layout.

```csharp
public sealed record TreemapRect : IEquatable<TreemapRect>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **TreemapRect**

## Implements

- [IEquatable\<TreemapRect\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### TreemapRect(double, double, double, double, SizeNode)

A positioned rectangle in the treemap layout.

**Parameters:**

- `X` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 
- `Y` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 
- `Width` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 
- `Height` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 
- `Node` ([SizeNode](/api/dotsider.core.analysis.models.sizenode/)): 

```csharp
public TreemapRect(double X, double Y, double Width, double Height, SizeNode Node)
```

## Properties

### Height

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double Height { get; init; }
```

### Node

**Returns:** [SizeNode](/api/dotsider.core.analysis.models.sizenode/)

```csharp
public SizeNode Node { get; init; }
```

### Width

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double Width { get; init; }
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

