---
title: "NativeSymbolInfo"
description: "The native symbols recovered from a binary, plus the provenance and status needed to explain the result. Symbols are ordered by VirtualAddress, which NativeSymbol%40) relies on to resolve an address to its containing symbol — the lookup the disassembly and hex views use to name code."
slug: api/dotsider.core.analysis.models.nativesymbolinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The native symbols recovered from a binary, plus the provenance and status needed to explain
the result. Symbols are ordered by [VirtualAddress](/api/dotsider.core.analysis.models.nativesymbol.virtualaddress/), which
NativeSymbol%40) relies on to resolve an address to its containing symbol —
the lookup the disassembly and hex views use to name code.

```csharp
public sealed record NativeSymbolInfo : IEquatable<NativeSymbolInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeSymbolInfo**

## Implements

- [IEquatable\<NativeSymbolInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeSymbolInfo(IReadOnlyList\<NativeSymbol\>, NativeSymbolSource, NativeSymbolStatus, string?, string?, NativeArchitecture, NativeSourceMap?)

The native symbols recovered from a binary, plus the provenance and status needed to explain
the result. Symbols are ordered by [VirtualAddress](/api/dotsider.core.analysis.models.nativesymbol.virtualaddress/), which
NativeSymbol%40) relies on to resolve an address to its containing symbol —
the lookup the disassembly and hex views use to name code.

**Parameters:**

- `Symbols` ([IReadOnlyList\<NativeSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The recovered symbols, sorted ascending by virtual address.
- `Source` ([NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)): Which reader produced the symbols.
- `Status` ([NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)): The probe outcome; explains an empty result.
- `Path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The symbol file that was read (PDB, .dbg, or dSYM inner file), or null for self/fallback sources.
- `Diagnostic` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A human-readable note on the outcome — the mismatch detail, the fallback reason, or null on a clean load.
- `Architecture` ([NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)): The instruction-set architecture of the code the symbols describe (the real selected slice for a fat Mach-O), or [Unknown](/api/dotsider.core.analysis.models.nativearchitecture.unknown/).
- `SourceMap` ([NativeSourceMap](/api/dotsider.core.analysis.models.nativesourcemap/)): The aggregated address→file:line map recovered from the sidecar, or null when the format carries no line data.

```csharp
public NativeSymbolInfo(IReadOnlyList<NativeSymbol> Symbols, NativeSymbolSource Source, NativeSymbolStatus Status, string? Path, string? Diagnostic, NativeArchitecture Architecture, NativeSourceMap? SourceMap)
```

### NativeSymbolInfo(IReadOnlyList\<NativeSymbol\>, NativeSymbolSource, NativeSymbolStatus, string?, string?)

The pre-#178 five-argument shape, preserved so existing construction sites keep compiling.
The architecture defaults to [Unknown](/api/dotsider.core.analysis.models.nativearchitecture.unknown/) and the source map to null.

**Parameters:**

- `symbols` ([IReadOnlyList\<NativeSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The recovered symbols, sorted ascending by virtual address.
- `source` ([NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)): Which reader produced the symbols.
- `status` ([NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)): The probe outcome.
- `path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The symbol file that was read, or null.
- `diagnostic` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A human-readable note on the outcome, or null.

```csharp
public NativeSymbolInfo(IReadOnlyList<NativeSymbol> symbols, NativeSymbolSource source, NativeSymbolStatus status, string? path, string? diagnostic)
```

## Properties

### Architecture

The instruction-set architecture of the code the symbols describe (the real selected slice for a fat Mach-O), or [Unknown](/api/dotsider.core.analysis.models.nativearchitecture.unknown/).

**Returns:** [NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)

```csharp
public NativeArchitecture Architecture { get; init; }
```

### Diagnostic

A human-readable note on the outcome — the mismatch detail, the fallback reason, or null on a clean load.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Diagnostic { get; init; }
```

### Path

The symbol file that was read (PDB, .dbg, or dSYM inner file), or null for self/fallback sources.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Path { get; init; }
```

### Source

Which reader produced the symbols.

**Returns:** [NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)

```csharp
public NativeSymbolSource Source { get; init; }
```

### SourceMap

The aggregated address→file:line map recovered from the sidecar, or null when the format carries no line data.

**Returns:** [NativeSourceMap](/api/dotsider.core.analysis.models.nativesourcemap/)

```csharp
public NativeSourceMap? SourceMap { get; init; }
```

### Status

The probe outcome; explains an empty result.

**Returns:** [NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)

```csharp
public NativeSymbolStatus Status { get; init; }
```

### Symbols

The recovered symbols, sorted ascending by virtual address.

**Returns:** [IReadOnlyList\<NativeSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NativeSymbol> Symbols { get; init; }
```

## Methods

### Deconstruct(out IReadOnlyList\<NativeSymbol\>, out NativeSymbolSource, out NativeSymbolStatus, out string?, out string?)

The pre-#178 five-output deconstruction, preserved alongside the generated seven-output one.

**Parameters:**

- `symbols` ([IReadOnlyList\<NativeSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The recovered symbols.
- `source` ([NativeSymbolSource](/api/dotsider.core.analysis.models.nativesymbolsource/)): Which reader produced the symbols.
- `status` ([NativeSymbolStatus](/api/dotsider.core.analysis.models.nativesymbolstatus/)): The probe outcome.
- `path` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The symbol file that was read, or null.
- `diagnostic` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A human-readable note on the outcome, or null.

```csharp
public void Deconstruct(out IReadOnlyList<NativeSymbol> symbols, out NativeSymbolSource source, out NativeSymbolStatus status, out string? path, out string? diagnostic)
```

### TryFindByAddress(ulong, out NativeSymbol)

Finds the symbol whose range contains virtualAddress. Binary-searches
the address-sorted list for the last symbol starting at or before the address, then
confirms the address falls within that symbol's size.

**Parameters:**

- `virtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The virtual address to resolve.
- `symbol` ([NativeSymbol](/api/dotsider.core.analysis.models.nativesymbol/)): The containing symbol when found.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

True when a symbol contains the address; otherwise false.

```csharp
public bool TryFindByAddress(ulong virtualAddress, out NativeSymbol symbol)
```

