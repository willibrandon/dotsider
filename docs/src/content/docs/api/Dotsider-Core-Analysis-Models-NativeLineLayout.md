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
public readonly record struct NativeLineLayout : IEquatable<NativeLineLayout>
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

