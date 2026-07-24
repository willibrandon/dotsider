---
title: "NativeSymbolReader"
description: "Reads a native binary's symbols — function names, addresses, and sizes — from its debug information, demangling ILC names back to managed names and merging the overlapping records that different symbol sources produce. Windows native PDBs, Linux DWARF, and macOS dSYM/nlist each feed the same merge and demangle pipeline through NativeSourceMap); when no symbols exist, unwind data still yields function boundaries at lower fidelity. The public entry points that dispatch on image format are added as each reader lands."
slug: api/dotsider.core.analysis.nativesymbolreader
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Reads a native binary's symbols — function names, addresses, and sizes — from its debug
information, demangling ILC names back to managed names and merging the overlapping records
that different symbol sources produce. Windows native PDBs, Linux DWARF, and macOS dSYM/nlist
each feed the same merge and demangle pipeline through NativeSourceMap); when no symbols
exist, unwind data still yields function boundaries at lower fidelity. The public entry points
that dispatch on image format are added as each reader lands.

```csharp
public static class NativeSymbolReader
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeSymbolReader**

## Methods

### Read(string, ReadOnlyMemory\<byte\>, IReadOnlyList\<RecoveredType\>)

Reads the native symbols of a binary, dispatching on image format. Managed and
unrecognized images return an empty result marked [NotApplicable](/api/dotsider.core.analysis.models.nativesymbolstatus.notapplicable/).
Malformed or oversized symbol data degrades to the applicable platform fallback and status.

**Parameters:**

- `imagePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The binary's path, used to probe for sidecar symbol files.
- `imageBytes` ([ReadOnlyMemory\<Byte\>](https://learn.microsoft.com/dotnet/api/system.readonlymemory-1)): The binary's raw bytes.
- `recoveredTypes` ([IReadOnlyList\<RecoveredType\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Types recovered from the binary's own metadata, for demangling.

**Returns:** [NativeSymbolInfo](/api/dotsider.core.analysis.models.nativesymbolinfo/)

```csharp
public static NativeSymbolInfo Read(string imagePath, ReadOnlyMemory<byte> imageBytes, IReadOnlyList<RecoveredType> recoveredTypes)
```

