---
title: "NativeInstruction"
description: "One decoded native instruction. The model is structured — bytes, structured operands, flow and target metadata, and source attribution — so navigation, JSON/MCP output, syntax decoration, and future diffing read facts rather than parse text. OperandText and the rendered listing line are projections; Address is the semantic key and DisplayLine the presentation key, mirroring IlInstruction for the shared IL-Inspector plumbing."
slug: api/dotsider.core.analysis.models.nativeinstruction
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One decoded native instruction. The model is structured — bytes, structured operands, flow and
target metadata, and source attribution — so navigation, JSON/MCP output, syntax decoration,
and future diffing read facts rather than parse text. [OperandText](/api/dotsider.core.analysis.models.nativeinstruction.operandtext/) and the
rendered listing line are projections; [Address](/api/dotsider.core.analysis.models.nativeinstruction.address/) is the semantic key and
[DisplayLine](/api/dotsider.core.analysis.models.nativeinstruction.displayline/) the presentation key, mirroring
[IlInstruction](/api/dotsider.core.analysis.models.ilinstruction/) for the shared IL-Inspector plumbing.

```csharp
public sealed record NativeInstruction : IEquatable<NativeInstruction>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeInstruction**

## Implements

- [IEquatable\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeInstruction(ulong, uint?, long?, IReadOnlyList\<byte\>, int, string, IReadOnlyList\<NativeOperand\>, string, NativeInstructionCategory, NativeFlowKind, ulong?, NativeTargetKind, string?, string?, int?, bool, int?, NativeLineLayout?)

One decoded native instruction. The model is structured — bytes, structured operands, flow and
target metadata, and source attribution — so navigation, JSON/MCP output, syntax decoration,
and future diffing read facts rather than parse text. [OperandText](/api/dotsider.core.analysis.models.nativeinstruction.operandtext/) and the
rendered listing line are projections; [Address](/api/dotsider.core.analysis.models.nativeinstruction.address/) is the semantic key and
[DisplayLine](/api/dotsider.core.analysis.models.nativeinstruction.displayline/) the presentation key, mirroring
[IlInstruction](/api/dotsider.core.analysis.models.ilinstruction/) for the shared IL-Inspector plumbing.

**Parameters:**

- `Address` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The instruction's virtual address.
- `Rva` ([Nullable\<UInt32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The PE relative virtual address, or null for non-PE images.
- `FileOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The file offset of the instruction's bytes, or null when not file-backed.
- `Bytes` ([IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The raw encoded bytes of exactly this instruction.
- `Length` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The encoded byte length (always exact, even for the fallback).
- `Mnemonic` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The instruction mnemonic (e.g. `mov`, `vaddps`, `bl`, or `.byte`/`.word` for the fallback).
- `Operands` ([IReadOnlyList\<NativeOperand\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The structured operands, in source order.
- `OperandText` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The rendered operand string, or empty when there are none.
- `Category` ([NativeInstructionCategory](/api/dotsider.core.analysis.models.nativeinstructioncategory/)): The instruction's coarse category.
- `Flow` ([NativeFlowKind](/api/dotsider.core.analysis.models.nativeflowkind/)): How the instruction affects control flow.
- `TargetAddress` ([Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The resolved absolute call/branch/data target, or null.
- `TargetKind` ([NativeTargetKind](/api/dotsider.core.analysis.models.nativetargetkind/)): What TargetAddress points at.
- `TargetName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The resolved target's display name (e.g. `Foo`, `Foo+0x12`, `loc_140001234`), or null.
- `SourceFile` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The source file for this address from the native source map, or null.
- `Line` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The source line for this address, or null.
- `IsFallback` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether this is a `.byte`/`.word` safety-net entry for undefined or corrupt bytes.
- `DisplayLine` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The 1-based rendered line number in formatted disassembly, or null.
- `Layout` ([Nullable\<NativeLineLayout\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The rendered-line column spans, set by the text formatter, or null.

```csharp
public NativeInstruction(ulong Address, uint? Rva, long? FileOffset, IReadOnlyList<byte> Bytes, int Length, string Mnemonic, IReadOnlyList<NativeOperand> Operands, string OperandText, NativeInstructionCategory Category, NativeFlowKind Flow, ulong? TargetAddress = null, NativeTargetKind TargetKind = NativeTargetKind.None, string? TargetName = null, string? SourceFile = null, int? Line = null, bool IsFallback = false, int? DisplayLine = null, NativeLineLayout? Layout = null)
```

## Properties

### Address

The instruction's virtual address.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong Address { get; init; }
```

### Bytes

The raw encoded bytes of exactly this instruction.

**Returns:** [IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<byte> Bytes { get; init; }
```

### Category

The instruction's coarse category.

**Returns:** [NativeInstructionCategory](/api/dotsider.core.analysis.models.nativeinstructioncategory/)

```csharp
public NativeInstructionCategory Category { get; init; }
```

### DisplayLine

The 1-based rendered line number in formatted disassembly, or null.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? DisplayLine { get; init; }
```

### FileOffset

The file offset of the instruction's bytes, or null when not file-backed.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? FileOffset { get; init; }
```

### Flow

How the instruction affects control flow.

**Returns:** [NativeFlowKind](/api/dotsider.core.analysis.models.nativeflowkind/)

```csharp
public NativeFlowKind Flow { get; init; }
```

### IsFallback

Whether this is a `.byte`/`.word` safety-net entry for undefined or corrupt bytes.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsFallback { get; init; }
```

### Layout

The rendered-line column spans, set by the text formatter, or null.

**Returns:** [Nullable\<NativeLineLayout\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public NativeLineLayout? Layout { get; init; }
```

### Length

The encoded byte length (always exact, even for the fallback).

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Length { get; init; }
```

### Line

The source line for this address, or null.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? Line { get; init; }
```

### Mnemonic

The instruction mnemonic (e.g. `mov`, `vaddps`, `bl`, or `.byte`/`.word` for the fallback).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Mnemonic { get; init; }
```

### Operands

The structured operands, in source order.

**Returns:** [IReadOnlyList\<NativeOperand\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NativeOperand> Operands { get; init; }
```

### OperandText

The rendered operand string, or empty when there are none.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string OperandText { get; init; }
```

### Rva

The PE relative virtual address, or null for non-PE images.

**Returns:** [Nullable\<UInt32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public uint? Rva { get; init; }
```

### SourceFile

The source file for this address from the native source map, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SourceFile { get; init; }
```

### TargetAddress

The resolved absolute call/branch/data target, or null.

**Returns:** [Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public ulong? TargetAddress { get; init; }
```

### TargetKind

What TargetAddress points at.

**Returns:** [NativeTargetKind](/api/dotsider.core.analysis.models.nativetargetkind/)

```csharp
public NativeTargetKind TargetKind { get; init; }
```

### TargetName

The resolved target's display name (e.g. `Foo`, `Foo+0x12`, `loc_140001234`), or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? TargetName { get; init; }
```

