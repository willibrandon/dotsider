---
title: "NativeTargetKind"
description: "What a decoded instruction's resolved TargetAddress points at, so the view can style and navigate a call/branch/data reference correctly."
slug: api/dotsider.core.analysis.models.nativetargetkind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

What a decoded instruction's resolved [TargetAddress](/api/dotsider.core.analysis.models.nativeinstruction.targetaddress/) points at,
so the view can style and navigate a call/branch/data reference correctly.

```csharp
public enum NativeTargetKind
```

## Fields

### Data

A data symbol (RIP-relative data reference, ADRP/ADR materialization).

**Returns:** [NativeTargetKind](/api/dotsider.core.analysis.models.nativetargetkind/)

```csharp
Data = 2
```

### Function

A function symbol (possibly at a non-zero offset into it).

**Returns:** [NativeTargetKind](/api/dotsider.core.analysis.models.nativetargetkind/)

```csharp
Function = 1
```

### Import

An imported symbol reached through the IAT, PLT/GOT, or a Mach-O stub.

**Returns:** [NativeTargetKind](/api/dotsider.core.analysis.models.nativetargetkind/)

```csharp
Import = 3
```

### LocalLabel

A synthesized label for a target inside the current function.

**Returns:** [NativeTargetKind](/api/dotsider.core.analysis.models.nativetargetkind/)

```csharp
LocalLabel = 4
```

### None

No resolvable target.

**Returns:** [NativeTargetKind](/api/dotsider.core.analysis.models.nativetargetkind/)

```csharp
None = 0
```

### Unresolved

A computed target that resolved to no known symbol.

**Returns:** [NativeTargetKind](/api/dotsider.core.analysis.models.nativetargetkind/)

```csharp
Unresolved = 5
```

