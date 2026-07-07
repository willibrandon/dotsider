---
title: "WasmDataSegmentInfo"
description: "One WebAssembly data segment."
slug: api/dotsider.core.analysis.models.wasmdatasegmentinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One WebAssembly data segment.

```csharp
public sealed record WasmDataSegmentInfo : IEquatable<WasmDataSegmentInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmDataSegmentInfo**

## Implements

- [IEquatable\<WasmDataSegmentInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmDataSegmentInfo(int, string, long, int)

One WebAssembly data segment.

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The data segment index.
- `Mode` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The decoded segment mode: active, passive, or active-explicit-memory.
- `FileOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The file offset where the segment's bytes begin.
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The segment byte size.

```csharp
public WasmDataSegmentInfo(int Index, string Mode, long FileOffset, int Size)
```

## Properties

### FileOffset

The file offset where the segment's bytes begin.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long FileOffset { get; init; }
```

### Index

The data segment index.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### Mode

The decoded segment mode: active, passive, or active-explicit-memory.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Mode { get; init; }
```

### Size

The segment byte size.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

