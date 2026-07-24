---
title: "NativeImportResolver"
description: "Resolves an indirect call/branch target that lands on an import slot to the imported symbol name, so a call [rip+disp] through the PE Import Address Table renders as KERNEL32!GetProcAddress, an ELF PLT stub jumping through its GOT slot renders as the bound dynamic symbol, and a Mach-O stub renders as its imported symbol — rather than an unresolved address. Built once per image, it maps each import slot's virtual address to its name. NativeSymbolRef%40) composes after the symbol resolver in NativeSymbol)."
slug: api/dotsider.core.analysis.disasm.nativeimportresolver
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Disasm`

**Assembly:** Dotsider.Core.dll

Resolves an indirect call/branch target that lands on an import slot to the imported symbol name,
so a `call [rip+disp]` through the PE Import Address Table renders as
`KERNEL32!GetProcAddress`, an ELF PLT stub jumping through its GOT slot renders as the bound
dynamic symbol, and a Mach-O stub renders as its imported symbol — rather than an unresolved
address. Built once per image, it maps each import slot's virtual address to its name.
NativeSymbolRef%40) composes after the symbol resolver in
NativeSymbol).

```csharp
public sealed class NativeImportResolver
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeImportResolver**

## Methods

### Build(ReadOnlyMemory\<byte\>, NativeArchitecture)

Builds the resolver from a binary's raw bytes, dispatching on the image format (PE, ELF, or
Mach-O), or null when the format carries no resolvable import slots or its import data is
malformed or oversized.

**Parameters:**

- `rawBytes` ([ReadOnlyMemory\<Byte\>](https://learn.microsoft.com/dotnet/api/system.readonlymemory-1)): The image's raw bytes.
- `architecture` ([NativeArchitecture](/api/dotsider.core.analysis.models.nativearchitecture/)): The selected architecture, used to pick the slice of a fat Mach-O.

**Returns:** [NativeImportResolver](/api/dotsider.core.analysis.disasm.nativeimportresolver/)

```csharp
public static NativeImportResolver? Build(ReadOnlyMemory<byte> rawBytes, NativeArchitecture architecture = NativeArchitecture.Unknown)
```

### TryResolve(ulong, out NativeSymbolRef)

Resolves an import-slot virtual address to its imported name.

**Parameters:**

- `targetVirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The address the indirect target points at (the IAT slot).
- `import` ([NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/)): The resolved import symbol on success.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool TryResolve(ulong targetVirtualAddress, out NativeSymbolRef import)
```

