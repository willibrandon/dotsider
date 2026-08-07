---
title: "SizeNodeKind"
description: "The granularity level of a SizeNode in the size breakdown tree. The kinds beyond Method appear only in Native AOT trees, built from an mstat report or, when none sits beside the binary, from its merged native symbols."
slug: api/dotsider.core.analysis.models.sizenodekind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The granularity level of a [SizeNode](/api/dotsider.core.analysis.models.sizenode/) in the size breakdown tree. The kinds
beyond [Method](/api/dotsider.core.analysis.models.sizenodekind.method/) appear only in Native AOT trees, built from an mstat report or,
when none sits beside the binary, from its merged native symbols.

```csharp
public enum SizeNodeKind
```

## Fields

### Assembly

Root node representing an entire assembly.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Assembly = 0
```

### Blob

A named global data region of a Native AOT binary.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Blob = 5
```

### Category

Grouping node for a Native AOT data category (blobs, frozen objects, and the like).

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Category = 4
```

### FrozenObject

An object frozen into a Native AOT binary at compile time.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
FrozenObject = 7
```

### Function

A native function sized from the binary's symbols — unlike [Method](/api/dotsider.core.analysis.models.sizenodekind.method/), there
is no IL body behind it to drill into.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Function = 10
```

### Method

Node representing a method within a type.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Method = 3
```

### MethodTable

A type's runtime MethodTable data in a Native AOT binary.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
MethodTable = 6
```

### Namespace

Node representing a namespace within an assembly.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Namespace = 1
```

### Resource

A manifest resource embedded in a Native AOT binary.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Resource = 9
```

### RvaField

A field's RVA data mapped into a Native AOT binary.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
RvaField = 8
```

### Type

Node representing a type within a namespace.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Type = 2
```
