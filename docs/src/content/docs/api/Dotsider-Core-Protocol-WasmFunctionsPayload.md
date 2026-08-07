---
title: "WasmFunctionsPayload"
description: "A WebAssembly function inventory. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.wasmfunctionspayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A WebAssembly function inventory.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WasmFunctionsPayload : IEquatable<WasmFunctionsPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmFunctionsPayload**

## Implements

- [IEquatable\<WasmFunctionsPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmFunctionsPayload(string, int, IReadOnlyList\<WasmFunctionPayload\>)

A WebAssembly function inventory.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `FilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FunctionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Functions` ([IReadOnlyList\<WasmFunctionPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public WasmFunctionsPayload(string FilePath, int FunctionCount, IReadOnlyList<WasmFunctionPayload> Functions)
```

## Properties

### FilePath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FilePath { get; init; }
```

### FunctionCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int FunctionCount { get; init; }
```

### Functions

**Returns:** [IReadOnlyList\<WasmFunctionPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmFunctionPayload> Functions { get; init; }
```

## Methods

### Deconstruct(out string, out int, out IReadOnlyList\<WasmFunctionPayload\>)

**Parameters:**

- `FilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FunctionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Functions` ([IReadOnlyList\<WasmFunctionPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string FilePath, out int FunctionCount, out IReadOnlyList<WasmFunctionPayload> Functions)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmFunctionsPayload?)

**Parameters:**

- `other` ([WasmFunctionsPayload](/api/dotsider.core.protocol.wasmfunctionspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmFunctionsPayload? other)
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

### operator !=(WasmFunctionsPayload?, WasmFunctionsPayload?)

**Parameters:**

- `left` ([WasmFunctionsPayload](/api/dotsider.core.protocol.wasmfunctionspayload/))
- `right` ([WasmFunctionsPayload](/api/dotsider.core.protocol.wasmfunctionspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmFunctionsPayload? left, WasmFunctionsPayload? right)
```

### operator ==(WasmFunctionsPayload?, WasmFunctionsPayload?)

**Parameters:**

- `left` ([WasmFunctionsPayload](/api/dotsider.core.protocol.wasmfunctionspayload/))
- `right` ([WasmFunctionsPayload](/api/dotsider.core.protocol.wasmfunctionspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmFunctionsPayload? left, WasmFunctionsPayload? right)
```
