---
title: "ManagedNativeIndex"
description: "Joins pre-ILC managed methods to the native evidence of the AOT image they were compiled into: native symbols (via IlcNameDemangler, keyed from real companion metadata instead of the binary's reduced recovered types) and mstat size rows. Built once, queried per-frame — every lookup is a dictionary hit."
slug: api/dotsider.core.analysis.managednativeindex
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Joins pre-ILC managed methods to the native evidence of the AOT image they were
compiled into: native symbols (via IlcNameDemangler, keyed from real
companion metadata instead of the binary's reduced recovered types) and mstat size
rows. Built once, queried per-frame — every lookup is a dictionary hit.

```csharp
public sealed class ManagedNativeIndex
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ManagedNativeIndex**

## Properties

### AmbiguousCount

Methods whose native evidence is shared with sibling overloads.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int AmbiguousCount { get; }
```

### ExactCount

Methods that own native evidence outright.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ExactCount { get; }
```

### Methods

Every managed method's correlation, in source order.

**Returns:** [IReadOnlyList\<MethodCorrelation\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MethodCorrelation> Methods { get; }
```

### MstatOnlyCount

Methods with mstat size evidence but no native symbol to disassemble.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MstatOnlyCount { get; }
```

### NotInImageCount

Methods with no native evidence — trimmed, fully inlined, or bodiless.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int NotInImageCount { get; }
```

### TotalCorrelatedSize

The correlated native bytes, deduplicated: every evidence pool — owned or shared —
contributes exactly once, with mstat sizes preferred over symbol sizes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long TotalCorrelatedSize { get; }
```

## Methods

### Build(IReadOnlyList\<ManagedMethodSource\>, IReadOnlyList\<NativeSymbol\>, MstatData?)

Builds the index from managed method sources, the image's native symbols, and its
mstat report. Deliberately data-shaped — no analyzer required — so synthetic inputs
exercise every join rule without real binaries.

**Parameters:**

- `sources` ([IReadOnlyList\<ManagedMethodSource\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The pre-ILC assemblies: the root managed input and any local references.
- `nativeSymbols` ([IReadOnlyList\<NativeSymbol\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The AOT image's native symbols (empty when no symbol source exists).
- `mstat` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/)): The image's mstat report, or null when absent.

**Returns:** [ManagedNativeIndex](/api/dotsider.core.analysis.managednativeindex/)

```csharp
public static ManagedNativeIndex Build(IReadOnlyList<ManagedMethodSource> sources, IReadOnlyList<NativeSymbol> nativeSymbols, MstatData? mstat)
```

### Find(string, int)

Finds a method's correlation by its assembly simple name and metadata token.
Tokens collide across assemblies, so the composite key is required.

**Parameters:**

- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly simple name the method is defined in.
- `methodToken` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The method's metadata token.

**Returns:** [MethodCorrelation](/api/dotsider.core.analysis.models.methodcorrelation/)

```csharp
public MethodCorrelation? Find(string assemblyName, int methodToken)
```

### FindByAddress(ulong)

Finds the correlation whose evidence contains the symbol at
virtualAddress, or null for uncorrelated (runtime/stub) code.

**Parameters:**

- `virtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The symbol's virtual address.

**Returns:** [MethodCorrelation](/api/dotsider.core.analysis.models.methodcorrelation/)

```csharp
public MethodCorrelation? FindByAddress(ulong virtualAddress)
```

### FindByNativeSymbol(NativeSymbol)

Finds the correlation a native symbol belongs to, keyed by its virtual address.
For a shared (overload) pool the first candidate is returned; its
[Status](/api/dotsider.core.analysis.models.methodcorrelation.status/) reveals the ambiguity.

**Parameters:**

- `symbol` ([NativeSymbol](/api/dotsider.core.analysis.models.nativesymbol/)): The native symbol to look up.

**Returns:** [MethodCorrelation](/api/dotsider.core.analysis.models.methodcorrelation/)

```csharp
public MethodCorrelation? FindByNativeSymbol(NativeSymbol symbol)
```

## Remarks

The join is grouped by `(assembly, declaring type, method name)`: ILC's mangling
collapses signatures, so overloads form one evidence pool that no single candidate
owns. A single-method group owns its evidence ([CorrelatedExact](/api/dotsider.core.analysis.models.methodcorrelationstatus.correlatedexact/),
several symbols meaning generic instantiations); a multi-method group is shared
([CorrelatedAmbiguous](/api/dotsider.core.analysis.models.methodcorrelationstatus.correlatedambiguous/)), reported on every sibling
but counted once in [TotalCorrelatedSize](/api/dotsider.core.analysis.managednativeindex.totalcorrelatedsize/). Overload-suffix assignment
(`_0`/`_1`) is never guessed — the same policy the demangler applies.

