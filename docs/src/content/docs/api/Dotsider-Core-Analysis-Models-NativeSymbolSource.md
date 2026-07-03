---
title: "NativeSymbolSource"
description: "Where a binary's native symbols came from. The three primary sources carry names and (mostly) sizes; the three fallback sources recover only function boundaries from unwind data and are lower fidelity — they can miss leaf and thunk functions."
slug: api/dotsider.core.analysis.models.nativesymbolsource
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Where a binary's native symbols came from. The three primary sources carry names and (mostly)
sizes; the three fallback sources recover only function boundaries from unwind data and are
lower fidelity — they can miss leaf and thunk functions.

```csharp
public enum NativeSymbolSource
```

## Fields

### Dsym

A macOS dSYM bundle (DWARF plus nlist stabs).

**Returns:** [NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)

```csharp
Dsym = 2
```

### Dwarf

DWARF debug info in an unstripped ELF binary or a `.dbg` sidecar.

**Returns:** [NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)

```csharp
Dwarf = 1
```

### EhFrameFallback

ELF `.eh_frame` unwind info — function boundaries only.

**Returns:** [NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)

```csharp
EhFrameFallback = 5
```

### FunctionStartsFallback

Mach-O `LC_FUNCTION_STARTS` — function boundaries only.

**Returns:** [NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)

```csharp
FunctionStartsFallback = 6
```

### MachONlist

The Mach-O symbol table (nlist) of the binary itself.

**Returns:** [NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)

```csharp
MachONlist = 3
```

### NativePdb

A matched Windows native PDB (MSF container).

**Returns:** [NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)

```csharp
NativePdb = 0
```

### PdataFallback

PE `.pdata` exception directory — function boundaries only.

**Returns:** [NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)

```csharp
PdataFallback = 4
```

