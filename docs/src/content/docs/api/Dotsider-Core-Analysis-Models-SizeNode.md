---
title: "SizeNode"
description: "A node in the size treemap hierarchy. Can be assembly, namespace, type, or method — or, for Native AOT trees, a data category and its entries."
slug: api/dotsider.core.analysis.models.sizenode
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A node in the size treemap hierarchy. Can be assembly, namespace, type, or method — or,
for Native AOT trees, a data category and its entries.

```csharp
public sealed record SizeNode : IEquatable<SizeNode>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeNode**

## Implements

- [IEquatable\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeNode(string, string, long, SizeNodeKind, IReadOnlyList\<SizeNode\>, string?, ulong?)

A node in the size treemap hierarchy. Can be assembly, namespace, type, or method — or,
for Native AOT trees, a data category and its entries.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Display name for this node.
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Fully qualified path from root (e.g., `Assembly/Namespace/Type`).
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Size in bytes attributed to this node.
- `Kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)): The granularity level of this node.
- `Children` ([IReadOnlyList\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Child nodes in the hierarchy.
- `AotNodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The ILC dependency-graph node name behind this entry, or null outside Native AOT trees.
The name matches a DGML node label, which is what makes "why is this in my binary"
answerable for the node.
- `NativeAddress` ([Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The node's virtual address when it maps to a Native AOT function, or null. Cross-view navigation
reads this typed field rather than scraping [FullPath](/api/dotsider.core.analysis.models.sizenode.fullpath/).

```csharp
public SizeNode(string Name, string FullPath, long Size, SizeNodeKind Kind, IReadOnlyList<SizeNode> Children, string? AotNodeName, ulong? NativeAddress)
```

### SizeNode(string, string, long, SizeNodeKind, IReadOnlyList\<SizeNode\>, string?)

The pre-#178 shape (five or six arguments), preserved so existing construction sites keep
compiling. [NativeAddress](/api/dotsider.core.analysis.models.sizenode.nativeaddress/) defaults to null.

**Parameters:**

- `name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Display name for this node.
- `fullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Fully qualified path from root.
- `size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Size in bytes attributed to this node.
- `kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)): The granularity level of this node.
- `children` ([IReadOnlyList\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Child nodes in the hierarchy.
- `aotNodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The ILC dependency-graph node name, or null.

```csharp
public SizeNode(string name, string fullPath, long size, SizeNodeKind kind, IReadOnlyList<SizeNode> children, string? aotNodeName = null)
```

## Properties

### AotNodeName

The ILC dependency-graph node name behind this entry, or null outside Native AOT trees.
The name matches a DGML node label, which is what makes "why is this in my binary"
answerable for the node.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AotNodeName { get; init; }
```

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

### NativeAddress

The node's virtual address when it maps to a Native AOT function, or null. Cross-view navigation
reads this typed field rather than scraping [FullPath](/api/dotsider.core.analysis.models.sizenode.fullpath/).

**Returns:** [Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public ulong? NativeAddress { get; init; }
```

### Size

Size in bytes attributed to this node.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

## Methods

### Deconstruct(out string, out string, out long, out SizeNodeKind, out IReadOnlyList\<SizeNode\>, out string?, out ulong?)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `Kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/))
- `Children` ([IReadOnlyList\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `AotNodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `NativeAddress` ([Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public void Deconstruct(out string Name, out string FullPath, out long Size, out SizeNodeKind Kind, out IReadOnlyList<SizeNode> Children, out string? AotNodeName, out ulong? NativeAddress)
```

### Deconstruct(out string, out string, out long, out SizeNodeKind, out IReadOnlyList\<SizeNode\>, out string?)

The pre-#178 six-output deconstruction, preserved alongside the generated seven-output one.

**Parameters:**

- `name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Display name.
- `fullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Fully qualified path from root.
- `size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Size in bytes.
- `kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)): The granularity level.
- `children` ([IReadOnlyList\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Child nodes.
- `aotNodeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The ILC dependency-graph node name, or null.

```csharp
public void Deconstruct(out string name, out string fullPath, out long size, out SizeNodeKind kind, out IReadOnlyList<SizeNode> children, out string? aotNodeName)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SizeNode?)

**Parameters:**

- `other` ([SizeNode](/api/dotsider.core.analysis.models.sizenode/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SizeNode? other)
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

### operator !=(SizeNode?, SizeNode?)

**Parameters:**

- `left` ([SizeNode](/api/dotsider.core.analysis.models.sizenode/))
- `right` ([SizeNode](/api/dotsider.core.analysis.models.sizenode/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SizeNode? left, SizeNode? right)
```

### operator ==(SizeNode?, SizeNode?)

**Parameters:**

- `left` ([SizeNode](/api/dotsider.core.analysis.models.sizenode/))
- `right` ([SizeNode](/api/dotsider.core.analysis.models.sizenode/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SizeNode? left, SizeNode? right)
```
