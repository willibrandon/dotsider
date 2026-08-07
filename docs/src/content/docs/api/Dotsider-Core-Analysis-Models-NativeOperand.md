---
title: "NativeOperand"
description: "One decoded operand of a NativeInstruction, carried structurally so navigation, JSON/MCP output, syntax decoration, and future diffing never parse the rendered text. The Text is the display projection; the typed fields describe what it renders."
slug: api/dotsider.core.analysis.models.nativeoperand
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One decoded operand of a [NativeInstruction](/api/dotsider.core.analysis.models.nativeinstruction/), carried structurally so navigation,
JSON/MCP output, syntax decoration, and future diffing never parse the rendered text. The
[Text](/api/dotsider.core.analysis.models.nativeoperand.text/) is the display projection; the typed fields describe what it renders.

```csharp
public sealed record NativeOperand : IEquatable<NativeOperand>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeOperand**

## Implements

- [IEquatable\<NativeOperand\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeOperand(NativeOperandKind, string, string?, long?, string?, string?, int, long, bool)

One decoded operand of a [NativeInstruction](/api/dotsider.core.analysis.models.nativeinstruction/), carried structurally so navigation,
JSON/MCP output, syntax decoration, and future diffing never parse the rendered text. The
[Text](/api/dotsider.core.analysis.models.nativeoperand.text/) is the display projection; the typed fields describe what it renders.

**Parameters:**

- `Kind` ([NativeOperandKind](/api/dotsider.core.analysis.models.nativeoperandkind/)): The operand's kind.
- `Text` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The rendered operand text (e.g. `rax`, `0x10`, `[rbp-0x8]`, `zmm1{k1}{z}`).
- `Register` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The register name when [Kind](/api/dotsider.core.analysis.models.nativeoperand.kind/) is [Register](/api/dotsider.core.analysis.models.nativeoperandkind.register/), else null.
- `Immediate` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The immediate value when [Kind](/api/dotsider.core.analysis.models.nativeoperand.kind/) is [Immediate](/api/dotsider.core.analysis.models.nativeoperandkind.immediate/), else null.
- `MemoryBase` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The base register of a memory reference, or null.
- `MemoryIndex` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The index register of a memory reference, or null.
- `MemoryScale` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The index scale (1/2/4/8) of a memory reference, or 0 when there is no index.
- `MemoryDisplacement` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The displacement of a memory reference, or 0.
- `IsRipRelative` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether a memory reference is x64 RIP-relative (the displacement is off the next instruction).

```csharp
public NativeOperand(NativeOperandKind Kind, string Text, string? Register = null, long? Immediate = null, string? MemoryBase = null, string? MemoryIndex = null, int MemoryScale = 0, long MemoryDisplacement = 0, bool IsRipRelative = false)
```

## Properties

### Immediate

The immediate value when [Kind](/api/dotsider.core.analysis.models.nativeoperand.kind/) is [Immediate](/api/dotsider.core.analysis.models.nativeoperandkind.immediate/), else null.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? Immediate { get; init; }
```

### IsRipRelative

Whether a memory reference is x64 RIP-relative (the displacement is off the next instruction).

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsRipRelative { get; init; }
```

### Kind

The operand's kind.

**Returns:** [NativeOperandKind](/api/dotsider.core.analysis.models.nativeoperandkind/)

```csharp
public NativeOperandKind Kind { get; init; }
```

### MemoryBase

The base register of a memory reference, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? MemoryBase { get; init; }
```

### MemoryDisplacement

The displacement of a memory reference, or 0.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long MemoryDisplacement { get; init; }
```

### MemoryIndex

The index register of a memory reference, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? MemoryIndex { get; init; }
```

### MemoryScale

The index scale (1/2/4/8) of a memory reference, or 0 when there is no index.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MemoryScale { get; init; }
```

### Register

The register name when [Kind](/api/dotsider.core.analysis.models.nativeoperand.kind/) is [Register](/api/dotsider.core.analysis.models.nativeoperandkind.register/), else null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Register { get; init; }
```

### Text

The rendered operand text (e.g. `rax`, `0x10`, `[rbp-0x8]`, `zmm1{k1}{z}`).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Text { get; init; }
```

## Methods

### Deconstruct(out NativeOperandKind, out string, out string?, out long?, out string?, out string?, out int, out long, out bool)

**Parameters:**

- `Kind` ([NativeOperandKind](/api/dotsider.core.analysis.models.nativeoperandkind/))
- `Text` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Register` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Immediate` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `MemoryBase` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MemoryIndex` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MemoryScale` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MemoryDisplacement` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `IsRipRelative` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))

```csharp
public void Deconstruct(out NativeOperandKind Kind, out string Text, out string? Register, out long? Immediate, out string? MemoryBase, out string? MemoryIndex, out int MemoryScale, out long MemoryDisplacement, out bool IsRipRelative)
```

### Equals(NativeOperand?)

**Parameters:**

- `other` ([NativeOperand](/api/dotsider.core.analysis.models.nativeoperand/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeOperand? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
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

### operator !=(NativeOperand?, NativeOperand?)

**Parameters:**

- `left` ([NativeOperand](/api/dotsider.core.analysis.models.nativeoperand/))
- `right` ([NativeOperand](/api/dotsider.core.analysis.models.nativeoperand/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeOperand? left, NativeOperand? right)
```

### operator ==(NativeOperand?, NativeOperand?)

**Parameters:**

- `left` ([NativeOperand](/api/dotsider.core.analysis.models.nativeoperand/))
- `right` ([NativeOperand](/api/dotsider.core.analysis.models.nativeoperand/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeOperand? left, NativeOperand? right)
```
