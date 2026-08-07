---
title: "DgmlNode"
description: "One node of an ILC dependency graph. The label is the compiler's node name — the same string an mstat size entry stores as its NodeName, which is how the two files join."
slug: api/dotsider.core.analysis.models.dgmlnode
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One node of an ILC dependency graph. The label is the compiler's node name — the same
string an mstat size entry stores as its `NodeName`, which is how the two files join.

```csharp
public sealed record DgmlNode : IEquatable<DgmlNode>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DgmlNode**

## Implements

- [IEquatable\<DgmlNode\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### DgmlNode(int, string)

One node of an ILC dependency graph. The label is the compiler's node name — the same
string an mstat size entry stores as its `NodeName`, which is how the two files join.

**Parameters:**

- `Id` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The node id, unique within the graph.
- `Label` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The node name.

```csharp
public DgmlNode(int Id, string Label)
```

## Properties

### Id

The node id, unique within the graph.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Id { get; init; }
```

### Label

The node name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Label { get; init; }
```

## Methods

### Deconstruct(out int, out string)

**Parameters:**

- `Id` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Label` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out int Id, out string Label)
```

### Equals(DgmlNode?)

**Parameters:**

- `other` ([DgmlNode](/api/dotsider.core.analysis.models.dgmlnode/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(DgmlNode? other)
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

### operator !=(DgmlNode?, DgmlNode?)

**Parameters:**

- `left` ([DgmlNode](/api/dotsider.core.analysis.models.dgmlnode/))
- `right` ([DgmlNode](/api/dotsider.core.analysis.models.dgmlnode/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(DgmlNode? left, DgmlNode? right)
```

### operator ==(DgmlNode?, DgmlNode?)

**Parameters:**

- `left` ([DgmlNode](/api/dotsider.core.analysis.models.dgmlnode/))
- `right` ([DgmlNode](/api/dotsider.core.analysis.models.dgmlnode/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(DgmlNode? left, DgmlNode? right)
```
