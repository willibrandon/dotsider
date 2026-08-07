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

## Methods

### Deconstruct(out int, out IReadOnlyList\<byte\>, out IReadOnlyList\<byte\>)

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ParamTypes` ([IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `ResultTypes` ([IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out int Index, out IReadOnlyList<byte> ParamTypes, out IReadOnlyList<byte> ResultTypes)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmTypeInfo?)

**Parameters:**

- `other` ([WasmTypeInfo](/api/dotsider.core.analysis.models.wasmtypeinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmTypeInfo? other)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(WasmTypeInfo?, WasmTypeInfo?)

**Parameters:**

- `left` ([WasmTypeInfo](/api/dotsider.core.analysis.models.wasmtypeinfo/))
- `right` ([WasmTypeInfo](/api/dotsider.core.analysis.models.wasmtypeinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmTypeInfo? left, WasmTypeInfo? right)
```

### operator ==(WasmTypeInfo?, WasmTypeInfo?)

**Parameters:**

- `left` ([WasmTypeInfo](/api/dotsider.core.analysis.models.wasmtypeinfo/))
- `right` ([WasmTypeInfo](/api/dotsider.core.analysis.models.wasmtypeinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmTypeInfo? left, WasmTypeInfo? right)
```
