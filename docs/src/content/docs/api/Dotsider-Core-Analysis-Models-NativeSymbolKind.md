---
title: "NativeSymbolKind"
description: "What a native symbol represents. Native AOT binaries carry compiler-generated code and data symbols beyond ordinary functions; this classification drives the Size Map's category grouping and the symbol view's presentation."
slug: api/dotsider.core.analysis.models.nativesymbolkind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

What a native symbol represents. Native AOT binaries carry compiler-generated code and data
symbols beyond ordinary functions; this classification drives the Size Map's category
grouping and the symbol view's presentation.

```csharp
public enum NativeSymbolKind
```

## Fields

### Boundary

A nameless function boundary recovered from unwind data when no symbols exist.

**Returns:** [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

```csharp
Boundary = 7
```

### Data

Other named data (readonly/writable data and the like).

**Returns:** [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

```csharp
Data = 6
```

### FrozenObject

A frozen (compile-time allocated) object, most often a string literal (`__Str_…`).

**Returns:** [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

```csharp
FrozenObject = 2
```

### Function

A compiled method body.

**Returns:** [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

```csharp
Function = 0
```

### GenericDictionary

A generic dictionary blob (`__GenericDict_…`).

**Returns:** [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

```csharp
GenericDictionary = 4
```

### MethodTable

A type's runtime MethodTable (vtable) — Windows `??_7…@@6B@` / Unix `_ZTV…`.

**Returns:** [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

```csharp
MethodTable = 1
```

### Statics

Static field storage (GC, non-GC, or thread statics).

**Returns:** [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

```csharp
Statics = 5
```

### Stub

A generic dictionary or an unboxing/other compiler stub.

**Returns:** [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

```csharp
Stub = 3
```
