---
title: "SizeNodeKind"
description: "The granularity level of a SizeNode in the size breakdown tree."
slug: api/dotsider.core.analysis.models.sizenodekind
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The granularity level of a [SizeNode](/api/dotsider.core.analysis.models.sizenode/) in the size breakdown tree.

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

### Method

Node representing a method within a type.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Method = 3
```

### Namespace

Node representing a namespace within an assembly.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Namespace = 1
```

### Type

Node representing a type within a namespace.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
Type = 2
```

