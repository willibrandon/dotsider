---
title: "IlInstruction"
description: "A single decoded IL (Intermediate Language) instruction."
slug: api/dotsider.core.analysis.models.ilinstruction
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A single decoded IL (Intermediate Language) instruction.

```csharp
public sealed record IlInstruction : IEquatable<IlInstruction>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **IlInstruction**

## Implements

- [IEquatable\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### IlInstruction(int, string, string, int?)

A single decoded IL (Intermediate Language) instruction.

**Parameters:**

- `Offset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The byte offset of this instruction within the method body.
- `OpCode` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The IL opcode mnemonic (e.g., "ldstr", "call", "ret").
- `Operand` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The decoded operand as a display string, or empty if the opcode takes no operand.
- `MetadataToken` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The raw metadata token for token-bearing operands (methods, fields, types), or null.

```csharp
public IlInstruction(int Offset, string OpCode, string Operand, int? MetadataToken = null)
```

## Properties

### MetadataToken

The raw metadata token for token-bearing operands (methods, fields, types), or null.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? MetadataToken { get; init; }
```

### Offset

The byte offset of this instruction within the method body.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Offset { get; init; }
```

### OpCode

The IL opcode mnemonic (e.g., "ldstr", "call", "ret").

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string OpCode { get; init; }
```

### Operand

The decoded operand as a display string, or empty if the opcode takes no operand.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Operand { get; init; }
```

