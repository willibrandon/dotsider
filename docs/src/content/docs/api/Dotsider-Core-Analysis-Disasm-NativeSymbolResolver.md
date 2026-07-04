---
title: "NativeSymbolResolver"
description: "Resolves a code or data virtual address to the symbol that contains it, so the disassembler can name call/branch/data targets. Returns false when no symbol covers the address. The out Offset lets the caller render Name+0x{offset} for a target that lands inside a symbol rather than at its start."
slug: api/dotsider.core.analysis.disasm.nativesymbolresolver
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Disasm`

**Assembly:** Dotsider.Core.dll

Resolves a code or data virtual address to the symbol that contains it, so the disassembler can
name call/branch/data targets. Returns false when no symbol covers the address. The out
[Offset](/api/dotsider.core.analysis.models.nativesymbolref.offset/) lets the caller render `Name+0x{offset}` for a target
that lands inside a symbol rather than at its start.

```csharp
public delegate bool NativeSymbolResolver(ulong virtualAddress, out NativeSymbolRef symbol)
```

