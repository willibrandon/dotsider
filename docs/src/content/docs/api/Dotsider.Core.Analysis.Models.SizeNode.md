---
title: "SizeNode"
description: "A node in the size treemap hierarchy. Can be assembly, namespace, type, or method."
slug: api/dotsider.core.analysis.models.sizenode
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

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 
- `Kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)): 
- `Children` ([IReadOnlyList\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): 

```csharp
public SizeNode(string Name, string FullPath, long Size, SizeNodeKind Kind, IReadOnlyList<SizeNode> Children)
```

## Properties

### Children

**Returns:** [IReadOnlyList\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeNode> Children { get; init; }
```

### FullPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullPath { get; init; }
```

### Kind

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
public SizeNodeKind Kind { get; init; }
```

### Name

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Size

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

