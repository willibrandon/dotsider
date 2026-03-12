---
title: "SizeNode"
description: "A node in the size treemap hierarchy. Can be assembly, namespace, type, or method."
slug: api/dotsider.core.analysis.models.sizenode
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A node in the size treemap hierarchy. Can be assembly, namespace, type, or method.

```csharp
public sealed record SizeNode : IEquatable<SizeNode>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeNode**

## Implements

- [IEquatable\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeNode(string, string, long, SizeNodeKind, IReadOnlyList\<SizeNode\>)

A node in the size treemap hierarchy. Can be assembly, namespace, type, or method.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Display name for this node.
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Fully qualified path from root (e.g., `Assembly/Namespace/Type`).
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Size in bytes attributed to this node.
- `Kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)): The granularity level of this node.
- `Children` ([IReadOnlyList\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Child nodes in the hierarchy.

```csharp
public SizeNode(string Name, string FullPath, long Size, SizeNodeKind Kind, IReadOnlyList<SizeNode> Children)
```

## Properties

### Children

Child nodes in the hierarchy.

**Returns:** [IReadOnlyList\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeNode> Children { get; init; }
```

### FullPath

Fully qualified path from root (e.g., `Assembly/Namespace/Type`).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullPath { get; init; }
```

### Kind

The granularity level of this node.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
public SizeNodeKind Kind { get; init; }
```

### Name

Display name for this node.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Size

Size in bytes attributed to this node.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

