---
title: "WasmElementSegmentInfo"
description: "One WebAssembly element segment declaration."
slug: api/dotsider.core.analysis.models.wasmelementsegmentinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One WebAssembly element segment declaration.

```csharp
public sealed record WasmElementSegmentInfo : IEquatable<WasmElementSegmentInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmElementSegmentInfo**

## Implements

- [IEquatable\<WasmElementSegmentInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmElementSegmentInfo(int, string, int?, string, int)

One WebAssembly element segment declaration.

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The zero-based element segment index.
- `Mode` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The decoded element segment mode.
- `TableIndex` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The table index when the mode records one.
- `ElementType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The reference type or element kind.
- `ElementCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of recorded element expressions or indices.

```csharp
public WasmElementSegmentInfo(int Index, string Mode, int? TableIndex, string ElementType, int ElementCount)
```

## Properties

### ElementCount

The number of recorded element expressions or indices.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ElementCount { get; init; }
```

### ElementType

The reference type or element kind.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string ElementType { get; init; }
```

### Index

The zero-based element segment index.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### Mode

The decoded element segment mode.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Mode { get; init; }
```

### TableIndex

The table index when the mode records one.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? TableIndex { get; init; }
```

## Methods

### Deconstruct(out int, out string, out int?, out string, out int)

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Mode` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TableIndex` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `ElementType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `ElementCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out int Index, out string Mode, out int? TableIndex, out string ElementType, out int ElementCount)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmElementSegmentInfo?)

**Parameters:**

- `other` ([WasmElementSegmentInfo](/api/dotsider.core.analysis.models.wasmelementsegmentinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmElementSegmentInfo? other)
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

### operator !=(WasmElementSegmentInfo?, WasmElementSegmentInfo?)

**Parameters:**

- `left` ([WasmElementSegmentInfo](/api/dotsider.core.analysis.models.wasmelementsegmentinfo/))
- `right` ([WasmElementSegmentInfo](/api/dotsider.core.analysis.models.wasmelementsegmentinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmElementSegmentInfo? left, WasmElementSegmentInfo? right)
```

### operator ==(WasmElementSegmentInfo?, WasmElementSegmentInfo?)

**Parameters:**

- `left` ([WasmElementSegmentInfo](/api/dotsider.core.analysis.models.wasmelementsegmentinfo/))
- `right` ([WasmElementSegmentInfo](/api/dotsider.core.analysis.models.wasmelementsegmentinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmElementSegmentInfo? left, WasmElementSegmentInfo? right)
```
