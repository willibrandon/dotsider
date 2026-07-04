---
title: "NativeOperandKind"
description: "The kind of a decoded operand, so consumers (JSON, decoration, diffing) read structure rather than parsing the rendered text."
slug: api/dotsider.core.analysis.models.nativeoperandkind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The kind of a decoded operand, so consumers (JSON, decoration, diffing) read structure rather
than parsing the rendered text.

```csharp
public enum NativeOperandKind
```

## Fields

### Immediate

An immediate constant.

**Returns:** [NativeOperandKind](/api/dotsider.core.analysis.models.nativeoperandkind/)

```csharp
Immediate = 1
```

### Memory

A memory reference (base/index/scale/displacement, or a PC-relative address).

**Returns:** [NativeOperandKind](/api/dotsider.core.analysis.models.nativeoperandkind/)

```csharp
Memory = 2
```

### Register

A register (GPR, vector, mask, predicate, or FP).

**Returns:** [NativeOperandKind](/api/dotsider.core.analysis.models.nativeoperandkind/)

```csharp
Register = 0
```

### RelativeTarget

A branch/call relative target (a code address).

**Returns:** [NativeOperandKind](/api/dotsider.core.analysis.models.nativeoperandkind/)

```csharp
RelativeTarget = 3
```

