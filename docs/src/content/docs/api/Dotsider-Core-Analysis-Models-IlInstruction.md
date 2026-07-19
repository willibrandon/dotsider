---
title: "IlInstruction"
description: "A single decoded IL (Intermediate Language) instruction."
slug: api/dotsider.core.analysis.models.ilinstruction
sidebar:
  order: 2
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

### IlInstruction(int, string, string, int?, string?, int?, int?, int?, int?, bool, string?, bool, int?, string?, int?)

A single decoded IL (Intermediate Language) instruction.

**Parameters:**

- `Offset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The byte offset of this instruction within the method body.
- `OpCode` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The IL opcode mnemonic (e.g., "ldstr", "call", "ret").
- `Operand` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The decoded operand as a display string, or empty if the opcode takes no operand.
- `MetadataToken` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The raw metadata token for token-bearing operands (methods, fields, types), or null.
- `SequenceDocument` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The source document for a sequence point starting at this instruction, or null.
- `SequenceStartLine` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The sequence point start line, or null.
- `SequenceStartColumn` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The sequence point start column, or null.
- `SequenceEndLine` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The sequence point end line, or null.
- `SequenceEndColumn` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The sequence point end column, or null.
- `SequenceHidden` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the sequence point is hidden.
- `SourceLinkUrl` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The Source Link URL resolved for the sequence point document, or null.
- `HasEmbeddedSource` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the sequence point document has embedded source.
- `LocalSlot` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The local variable slot referenced by this instruction, or null.
- `LocalName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The active PDB local variable name for LocalSlot, or null.
- `DisplayLine` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The 1-based rendered line number in formatted disassembly, or null.

```csharp
public IlInstruction(int Offset, string OpCode, string Operand, int? MetadataToken = null, string? SequenceDocument = null, int? SequenceStartLine = null, int? SequenceStartColumn = null, int? SequenceEndLine = null, int? SequenceEndColumn = null, bool SequenceHidden = false, string? SourceLinkUrl = null, bool HasEmbeddedSource = false, int? LocalSlot = null, string? LocalName = null, int? DisplayLine = null)
```

## Properties

### DisplayLine

The 1-based rendered line number in formatted disassembly, or null.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? DisplayLine { get; init; }
```

### HasEmbeddedSource

Whether the sequence point document has embedded source.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasEmbeddedSource { get; init; }
```

### IsMalformed

Gets or initializes a value indicating whether this is the terminal marker for malformed IL.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsMalformed { get; init; }
```

### LocalName

The active PDB local variable name for LocalSlot, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? LocalName { get; init; }
```

### LocalSlot

The local variable slot referenced by this instruction, or null.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? LocalSlot { get; init; }
```

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

### SequenceDocument

The source document for a sequence point starting at this instruction, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SequenceDocument { get; init; }
```

### SequenceEndColumn

The sequence point end column, or null.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? SequenceEndColumn { get; init; }
```

### SequenceEndLine

The sequence point end line, or null.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? SequenceEndLine { get; init; }
```

### SequenceHidden

Whether the sequence point is hidden.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool SequenceHidden { get; init; }
```

### SequenceStartColumn

The sequence point start column, or null.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? SequenceStartColumn { get; init; }
```

### SequenceStartLine

The sequence point start line, or null.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? SequenceStartLine { get; init; }
```

### SourceLinkUrl

The Source Link URL resolved for the sequence point document, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SourceLinkUrl { get; init; }
```

