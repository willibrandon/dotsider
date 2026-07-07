---
title: "WasmTypeInfo"
description: "One WebAssembly function type from the type section."
slug: api/dotsider.core.analysis.models.wasmtypeinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One WebAssembly function type from the type section.

```csharp
public sealed record WasmTypeInfo : IEquatable<WasmTypeInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmTypeInfo**

## Implements

- [IEquatable\<WasmTypeInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmTypeInfo(int, IReadOnlyList\<byte\>, IReadOnlyList\<byte\>)

One WebAssembly function type from the type section.

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The zero-based type index.
- `ParamTypes` ([IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The raw WebAssembly parameter value-type bytes.
- `ResultTypes` ([IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The raw WebAssembly result value-type bytes.

```csharp
public WasmTypeInfo(int Index, IReadOnlyList<byte> ParamTypes, IReadOnlyList<byte> ResultTypes)
```

## Properties

### Index

The zero-based type index.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### ParamTypes

The raw WebAssembly parameter value-type bytes.

**Returns:** [IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<byte> ParamTypes { get; init; }
```

### ResultTypes

The raw WebAssembly result value-type bytes.

**Returns:** [IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<byte> ResultTypes { get; init; }
```

