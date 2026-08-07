---
title: "MstatSectionKind"
description: "The report section a normalized MstatSizeEntry came from. Each section has its own identity key shape and attribution rules; see MstatSizeIndex."
slug: api/dotsider.core.analysis.models.mstatsectionkind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The report section a normalized [MstatSizeEntry](/api/dotsider.core.analysis.models.mstatsizeentry/) came from. Each section has its
own identity key shape and attribution rules; see [MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/).

```csharp
public enum MstatSectionKind
```

## Fields

### Blob

A named global data region.

**Returns:** [MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/)

```csharp
Blob = 2
```

### FrozenObject

An object frozen into the image at compile time.

**Returns:** [MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/)

```csharp
FrozenObject = 3
```

### Method

A compiled method body (code + GC info + EH info bytes).

**Returns:** [MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/)

```csharp
Method = 0
```

### MethodTable

A constructed type's MethodTable data.

**Returns:** [MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/)

```csharp
MethodTable = 1
```

### Resource

An embedded manifest resource.

**Returns:** [MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/)

```csharp
Resource = 5
```

### RvaField

A field's RVA data mapped into the image.

**Returns:** [MstatSectionKind](/api/dotsider.core.analysis.models.mstatsectionkind/)

```csharp
RvaField = 4
```
