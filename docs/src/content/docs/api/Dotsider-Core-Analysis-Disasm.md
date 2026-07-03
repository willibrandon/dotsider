---
title: "Dotsider.Core.Analysis.Disasm"
slug: api/dotsider.core.analysis.disasm
sidebar:
  order: 1
---

## Classes

### [NativeDisassembler](/api/dotsider.core.analysis.disasm.nativedisassembler/)

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

### [NativeImportResolver](/api/dotsider.core.analysis.disasm.nativeimportresolver/)

Resolves an indirect call/branch target that lands on an import slot to the imported symbol name,
so a `call [rip+disp]` through the PE Import Address Table renders as
`KERNEL32!GetProcAddress` rather than an unresolved address. Built once per image from the
import directory (PE IAT today; ELF PLT/GOT and Mach-O stubs are the planned extensions), it maps
each IAT slot's virtual address to its `MODULE!Function` name. NativeSymbolRef%40)
composes after the symbol resolver in NativeSymbol).

```csharp
public sealed class NativeImportResolver
```

## Structs

### [NativeSymbolName](/api/dotsider.core.analysis.disasm.nativesymbolname/)

Splits a recovered managed name (as joined by the native symbol reader, e.g.
`System.Text.StringBuilder.Append(char)`) into its namespace, declaring type, and member,
so the native IL-inspector tree can bucket functions the same namespace → type → method way the
managed tree does. The parse is signature-aware (it ignores the parameter list) and handles
nested types (`+`) and generic arity markers.

```csharp
public readonly record struct NativeSymbolName : IEquatable<NativeSymbolName>
```

## Delegates

### [NativeSymbolResolver](/api/dotsider.core.analysis.disasm.nativesymbolresolver/)

Resolves a code or data virtual address to the symbol that contains it, so the disassembler can
name call/branch/data targets. Returns false when no symbol covers the address. The out
[Offset](/api/dotsider.core.analysis.models.nativesymbolref.offset/) lets the caller render `Name+0x{offset}` for a target
that lands inside a symbol rather than at its start.

**Parameters:**

- `virtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The target virtual address to resolve.
- `symbol` ([NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/)): The containing symbol reference when found.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

True when a symbol contains virtualAddress; otherwise false.

```csharp
public delegate bool NativeSymbolResolver(ulong virtualAddress, out NativeSymbolRef symbol)
```

