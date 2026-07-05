---
title: "MethodCorrelationStatus"
description: "How a pre-ILC managed method relates to the native image it was compiled into."
slug: api/dotsider.core.analysis.models.methodcorrelationstatus
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

How a pre-ILC managed method relates to the native image it was compiled into.

```csharp
public enum MethodCorrelationStatus
```

## Fields

### CorrelatedAmbiguous

Native evidence exists but is shared with sibling overloads — ILC's overload
suffixes cannot be assigned back to a specific signature, so no candidate owns it.

**Returns:** [MethodCorrelationStatus](/api/dotsider.core.analysis.models.methodcorrelationstatus/)

```csharp
CorrelatedAmbiguous = 1
```

### CorrelatedByMstatOnly

The only evidence is mstat size data: the method was compiled, but no native symbol
is available to disassemble (size only; no native symbol).

**Returns:** [MethodCorrelationStatus](/api/dotsider.core.analysis.models.methodcorrelationstatus/)

```csharp
CorrelatedByMstatOnly = 2
```

### CorrelatedExact

The method owns its native evidence outright: one or more native symbols (several
mean generic instantiations) and any matching mstat rows are unambiguously its own.

**Returns:** [MethodCorrelationStatus](/api/dotsider.core.analysis.models.methodcorrelationstatus/)

```csharp
CorrelatedExact = 0
```

### NotInNativeImage

No native evidence at all — the method was trimmed away, fully inlined, or never
had a body (abstract/extern).

**Returns:** [MethodCorrelationStatus](/api/dotsider.core.analysis.models.methodcorrelationstatus/)

```csharp
NotInNativeImage = 3
```

