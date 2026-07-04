---
title: "NativeDisassembler"
description: "Disassembles a native code window into NativeInstructions and a rendered listing, dispatching to the table-driven x86-64 and A64 decoders. The listing mirrors the IL disassembly shape (IlDisassembler.DisassembleWithText): an optional header, then one line per instruction, loc_…: labels for intra-function targets, and each rendered line's column spans recorded on Layout so the TUI decorates structurally. Call/branch/data targets are resolved to names through a NativeSymbolResolver. A byte the decoder cannot recognize renders as an exact-width .byte/.word safety net that never desyncs the listing."
slug: api/dotsider.core.analysis.disasm.nativedisassembler
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Disasm`

**Assembly:** Dotsider.Core.dll

Disassembles a native code window into [NativeInstruction](/api/dotsider.core.analysis.models.nativeinstruction/)s and a rendered listing,
dispatching to the table-driven x86-64 and A64 decoders. The listing mirrors the IL disassembly
shape (`IlDisassembler.DisassembleWithText`): an optional header, then one line per
instruction, `loc_…:` labels for intra-function targets, and each rendered line's column
spans recorded on [Layout](/api/dotsider.core.analysis.models.nativeinstruction.layout/) so the TUI decorates structurally.
Call/branch/data targets are resolved to names through a [NativeSymbolResolver](/api/dotsider.core.analysis.disasm.nativesymbolresolver/).
A byte the decoder cannot recognize renders as an exact-width `.byte`/`.word` safety
net that never desyncs the listing.

```csharp
public static class NativeDisassembler
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeDisassembler**

## Methods

### Disassemble(ReadOnlySpan\<byte\>, ulong, NativeArchitecture, NativeSymbolResolver?)

Decodes a code window into instructions, resolving call/branch/data targets to names and
synthesizing labels for intra-window targets.

**Parameters:**

- `code` ([ReadOnlySpan\<Byte\>](https://learn.microsoft.com/dotnet/api/system.readonlyspan-1)): The exact code bytes of the region to disassemble.
- `baseAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The virtual address the first byte maps to.
- `arch` ([NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)): The instruction-set architecture to decode as.
- `resolver` ([NativeSymbolResolver](/api/dotsider.core.analysis.disasm.nativesymbolresolver/)): Resolves a target address to a symbol name, or null for no naming.

**Returns:** [IReadOnlyList\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public static IReadOnlyList<NativeInstruction> Disassemble(ReadOnlySpan<byte> code, ulong baseAddress, NativeArchitecture arch, NativeSymbolResolver? resolver = null)
```

### DisassembleSymbol(AssemblyAnalyzer, NativeSymbol)

The convenience the view, CLI, MCP, and session share: disassembles one recovered native
symbol from its owning analyzer. It slices the symbol's bytes, takes the
architecture from the recovered symbol info (the real selected slice), resolves call/branch/
data targets through the other symbols, stamps each instruction's source location from the
symbol info's source map, and renders a header with the symbol name and its file:line.
Returns null when the symbol has no file-backed bytes or the architecture is unknown.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The analyzer that recovered the symbol.
- `symbol` ([NativeSymbol](/api/dotsider.core.analysis.models.nativesymbol/)): The symbol to disassemble.

**Returns:** [Nullable\<String, NativeInstruction\>, Int32\>\>](https://learn.microsoft.com/dotnet/api/system.nullable-3)

```csharp
public static (string Text, IReadOnlyList<NativeInstruction> Instructions, int HeaderLineCount)? DisassembleSymbol(AssemblyAnalyzer analyzer, NativeSymbol symbol)
```

### DisassembleWithText(ReadOnlySpan\<byte\>, ulong, NativeArchitecture, string?, NativeSymbolResolver?)

Disassembles a code window and renders it to text, returning the text, the instruction list
(each carrying its 1-based [DisplayLine](/api/dotsider.core.analysis.models.nativeinstruction.displayline/) and
[Layout](/api/dotsider.core.analysis.models.nativeinstruction.layout/)), and the header line count.

**Parameters:**

- `code` ([ReadOnlySpan\<Byte\>](https://learn.microsoft.com/dotnet/api/system.readonlyspan-1)): The exact code bytes of the region to disassemble.
- `baseAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The virtual address the first byte maps to.
- `arch` ([NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)): The instruction-set architecture to decode as.
- `header` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Optional header lines (without a trailing blank), or null.
- `resolver` ([NativeSymbolResolver](/api/dotsider.core.analysis.disasm.nativesymbolresolver/)): Resolves a target address to a symbol name, or null for no naming.

**Returns:** [ValueTuple\<String, NativeInstruction\>, Int32\>](https://learn.microsoft.com/dotnet/api/system.valuetuple-3)

```csharp
public static (string Text, IReadOnlyList<NativeInstruction> Instructions, int HeaderLineCount) DisassembleWithText(ReadOnlySpan<byte> code, ulong baseAddress, NativeArchitecture arch, string? header = null, NativeSymbolResolver? resolver = null)
```

### FindExecutableSymbols(NativeSymbolInfo, string)

Resolves a disassembly target — a hex/decimal virtual address or a symbol name — to the
matching executable symbols, so callers report an exact hit, an ambiguity, or a miss the same
way. A hex `0x…` or decimal address resolves through the containing symbol; a name prefers
an exact managed-name match, then the raw symbol name, then a suffix match.

**Parameters:**

- `info` ([NativeSymbolInfo](/api/dotsider.core.analysis.models.nativesymbolinfo/)): The recovered native symbols.
- `target` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The address or name to resolve.

**Returns:** [IReadOnlyList\<NativeSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public static IReadOnlyList<NativeSymbol> FindExecutableSymbols(NativeSymbolInfo info, string target)
```

