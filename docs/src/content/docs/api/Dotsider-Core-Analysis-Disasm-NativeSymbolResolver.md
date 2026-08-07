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

## Constructors

### NativeSymbolResolver(object, nint)

**Parameters:**

- `object` ([Object](https://learn.microsoft.com/dotnet/api/system.object))
- `method` ([IntPtr](https://learn.microsoft.com/dotnet/api/system.intptr))

```csharp
public NativeSymbolResolver(object @object, nint method)
```

## Methods

### BeginInvoke(ulong, out NativeSymbolRef, AsyncCallback, object)

**Parameters:**

- `virtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `symbol` ([NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/))
- `callback` ([AsyncCallback](https://learn.microsoft.com/dotnet/api/system.asynccallback))
- `object` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [IAsyncResult](https://learn.microsoft.com/dotnet/api/system.iasyncresult)

```csharp
public virtual IAsyncResult BeginInvoke(ulong virtualAddress, out NativeSymbolRef symbol, AsyncCallback callback, object @object)
```

### EndInvoke(out NativeSymbolRef, IAsyncResult)

**Parameters:**

- `symbol` ([NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/))
- `result` ([IAsyncResult](https://learn.microsoft.com/dotnet/api/system.iasyncresult))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public virtual bool EndInvoke(out NativeSymbolRef symbol, IAsyncResult result)
```

### Invoke(ulong, out NativeSymbolRef)

**Parameters:**

- `virtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `symbol` ([NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public virtual bool Invoke(ulong virtualAddress, out NativeSymbolRef symbol)
```
