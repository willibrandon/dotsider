---
title: "NativeSymbol"
description: "One native symbol recovered from a binary: a function, a compiler-generated data blob, or a nameless boundary. The address is carried in every form a consumer might need — virtual address for display and cross-symbol ordering, PE RVA, file offset when the address is file-backed, and the containing section — so the UI, hex views, and disassembly never have to recompute a mapping."
slug: api/dotsider.core.analysis.models.nativesymbol
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One native symbol recovered from a binary: a function, a compiler-generated data blob, or a
nameless boundary. The address is carried in every form a consumer might need — virtual
address for display and cross-symbol ordering, PE RVA, file offset when the address is
file-backed, and the containing section — so the UI, hex views, and disassembly never have
to recompute a mapping.

```csharp
public sealed record NativeSymbol : IEquatable<NativeSymbol>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeSymbol**

## Implements

- [IEquatable\<NativeSymbol\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeSymbol(string, string?, ulong, uint?, long?, string?, long, NativeSymbolKind, string?, int?, bool, IReadOnlyList\<string\>)

One native symbol recovered from a binary: a function, a compiler-generated data blob, or a
nameless boundary. The address is carried in every form a consumer might need — virtual
address for display and cross-symbol ordering, PE RVA, file offset when the address is
file-backed, and the containing section — so the UI, hex views, and disassembly never have
to recompute a mapping.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The raw symbol name (mangled for managed code), or a synthesized `sub_…` for a boundary.
- `ManagedName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The managed name joined from the binary's recovered metadata, or null when no join exists. Overloads share a name, so this alone does not pin a member — [IsExactMatch](/api/dotsider.core.analysis.models.nativesymbol.isexactmatch/) is the precision flag.
- `VirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The symbol's virtual address (image base + RVA on PE; the symbol VA on ELF/Mach-O).
- `Rva` ([Nullable\<UInt32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The PE relative virtual address, or null for non-PE images.
- `FileOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The file offset the address maps to, or null when the symbol is not file-backed.
- `Section` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The containing section's name, or null when it could not be determined.
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The symbol's size in bytes, derived when the format does not record it directly.
- `Kind` ([NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)): What the symbol represents.
- `SourceFile` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The declaring source file, when debug line info is present.
- `Line` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The declaring source line, when debug line info is present.
- `IsExactMatch` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether [ManagedName](/api/dotsider.core.analysis.models.nativesymbol.managedname/) identifies exactly one recovered member; false when the join is ambiguous (overloads sharing a name, or an overload-suffix join).
- `Aliases` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Alternate names that resolved to the same address and were merged into this symbol.

```csharp
public NativeSymbol(string Name, string? ManagedName, ulong VirtualAddress, uint? Rva, long? FileOffset, string? Section, long Size, NativeSymbolKind Kind, string? SourceFile, int? Line, bool IsExactMatch, IReadOnlyList<string> Aliases)
```

## Properties

### Aliases

Alternate names that resolved to the same address and were merged into this symbol.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> Aliases { get; init; }
```

### FileOffset

The file offset the address maps to, or null when the symbol is not file-backed.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? FileOffset { get; init; }
```

### IsExactMatch

Whether [ManagedName](/api/dotsider.core.analysis.models.nativesymbol.managedname/) identifies exactly one recovered member; false when the join is ambiguous (overloads sharing a name, or an overload-suffix join).

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsExactMatch { get; init; }
```

### Kind

What the symbol represents.

**Returns:** [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

```csharp
public NativeSymbolKind Kind { get; init; }
```

### Line

The declaring source line, when debug line info is present.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? Line { get; init; }
```

### ManagedName

The managed name joined from the binary's recovered metadata, or null when no join exists. Overloads share a name, so this alone does not pin a member — [IsExactMatch](/api/dotsider.core.analysis.models.nativesymbol.isexactmatch/) is the precision flag.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ManagedName { get; init; }
```

### Name

The raw symbol name (mangled for managed code), or a synthesized `sub_…` for a boundary.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Rva

The PE relative virtual address, or null for non-PE images.

**Returns:** [Nullable\<UInt32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public uint? Rva { get; init; }
```

### Section

The containing section's name, or null when it could not be determined.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Section { get; init; }
```

### Size

The symbol's size in bytes, derived when the format does not record it directly.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### SourceFile

The declaring source file, when debug line info is present.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SourceFile { get; init; }
```

### VirtualAddress

The symbol's virtual address (image base + RVA on PE; the symbol VA on ELF/Mach-O).

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong VirtualAddress { get; init; }
```

