---
title: "NativeSymbolStatus"
description: "The outcome of probing a binary for native symbols. When no symbols are returned, the status distinguishes the reasons — missing, mismatched, corrupt, ambiguous, or fallback-only — so callers can explain the result instead of showing an empty table with no cause."
slug: api/dotsider.core.analysis.models.nativesymbolstatus
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The outcome of probing a binary for native symbols. When no symbols are returned, the status
distinguishes the reasons — missing, mismatched, corrupt, ambiguous, or fallback-only — so
callers can explain the result instead of showing an empty table with no cause.

```csharp
public enum NativeSymbolStatus
```

## Fields

### AmbiguousImage

A fat/universal binary offered no slice that could be disambiguated.

**Returns:** [NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)

```csharp
AmbiguousImage = 5
```

### CorruptSymbolFile

A symbol file was found and matched, but could not be parsed.

**Returns:** [NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)

```csharp
CorruptSymbolFile = 3
```

### FallbackOnly

Only nameless function boundaries were recovered from unwind data.

**Returns:** [NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)

```csharp
FallbackOnly = 4
```

### IdMismatch

A symbol file was found but its identity did not match the binary.

**Returns:** [NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)

```csharp
IdMismatch = 2
```

### Loaded

Named symbols loaded from a primary source.

**Returns:** [NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)

```csharp
Loaded = 0
```

### NoSymbolFile

No symbol file was found beside the binary and no fallback applied.

**Returns:** [NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)

```csharp
NoSymbolFile = 1
```

### NotApplicable

The binary is managed or otherwise has no native symbols to read.

**Returns:** [NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)

```csharp
NotApplicable = 6
```

