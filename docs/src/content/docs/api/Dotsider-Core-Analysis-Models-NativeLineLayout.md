---
title: "NativeLineLayout"
description: "The column ranges of the mnemonic, operands, and target within a rendered disassembly line, set by NativeDisassembler's text formatter. The TUI decoration providers highlight and hit-test by these spans rather than re-parsing the line, so the rendered text stays a pure projection of the structured instruction."
slug: api/dotsider.core.analysis.models.nativelinelayout
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The column ranges of the mnemonic, operands, and target within a rendered disassembly line,
set by [NativeDisassembler](/api/dotsider.core.analysis.disasm.nativedisassembler/)'s text formatter. The
TUI decoration providers highlight and hit-test by these spans rather than re-parsing the line,
so the rendered text stays a pure projection of the structured instruction.

```csharp
public readonly struct NativeLineLayout : IEquatable<NativeLineLayout>
```

## Implements

- [IEquatable\<NativeLineLayout\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeLineLayout(int, int, int, int, int, int)

The column ranges of the mnemonic, operands, and target within a rendered disassembly line,
set by [NativeDisassembler](/api/dotsider.core.analysis.disasm.nativedisassembler/)'s text formatter. The
TUI decoration providers highlight and hit-test by these spans rather than re-parsing the line,
so the rendered text stays a pure projection of the structured instruction.

**Parameters:**

- `MnemonicStart` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The column (0-based) where the mnemonic begins in the rendered line.
- `MnemonicLength` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The mnemonic's length in characters.
- `OperandsStart` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The column where the operand text begins, or -1 when there are no operands.
- `OperandsLength` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The operand text's length, or 0.
- `TargetStart` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The column where the resolved-target comment begins, or -1 when there is none.
- `TargetLength` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The target comment's length, or 0.

```csharp
public NativeLineLayout(int MnemonicStart, int MnemonicLength, int OperandsStart, int OperandsLength, int TargetStart, int TargetLength)
```

## Properties

### MnemonicLength

The mnemonic's length in characters.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MnemonicLength { get; init; }
```

### MnemonicStart

The column (0-based) where the mnemonic begins in the rendered line.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MnemonicStart { get; init; }
```

### OperandsLength

The operand text's length, or 0.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int OperandsLength { get; init; }
```

### OperandsStart

The column where the operand text begins, or -1 when there are no operands.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int OperandsStart { get; init; }
```

### TargetLength

The target comment's length, or 0.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TargetLength { get; init; }
```

### TargetStart

The column where the resolved-target comment begins, or -1 when there is none.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TargetStart { get; init; }
```

## Methods

### Deconstruct(out int, out int, out int, out int, out int, out int)

**Parameters:**

- `MnemonicStart` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MnemonicLength` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `OperandsStart` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `OperandsLength` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TargetStart` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TargetLength` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out int MnemonicStart, out int MnemonicLength, out int OperandsStart, out int OperandsLength, out int TargetStart, out int TargetLength)
```

### Equals(NativeLineLayout)

**Parameters:**

- `other` ([NativeLineLayout](/api/dotsider.core.analysis.models.nativelinelayout/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeLineLayout other)
```

### Equals(object)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object obj)
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

### operator !=(NativeLineLayout, NativeLineLayout)

**Parameters:**

- `left` ([NativeLineLayout](/api/dotsider.core.analysis.models.nativelinelayout/))
- `right` ([NativeLineLayout](/api/dotsider.core.analysis.models.nativelinelayout/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeLineLayout left, NativeLineLayout right)
```

### operator ==(NativeLineLayout, NativeLineLayout)

**Parameters:**

- `left` ([NativeLineLayout](/api/dotsider.core.analysis.models.nativelinelayout/))
- `right` ([NativeLineLayout](/api/dotsider.core.analysis.models.nativelinelayout/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeLineLayout left, NativeLineLayout right)
```
