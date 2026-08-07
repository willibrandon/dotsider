---
title: "IlDisassembler"
description: "Decodes IL (Intermediate Language) method bodies into human-readable instruction sequences."
slug: api/dotsider.core.analysis.ildisassembler
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Decodes IL (Intermediate Language) method bodies into human-readable instruction sequences.

```csharp
public sealed class IlDisassembler
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **IlDisassembler**

## Constructors

### IlDisassembler(AssemblyAnalyzer)

Decodes IL (Intermediate Language) method bodies into human-readable instruction sequences.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): Provides the assembly metadata and method bodies to disassemble.

```csharp
public IlDisassembler(AssemblyAnalyzer analyzer)
```

## Methods

### Disassemble(MethodDefInfo)

Disassembles a method's IL body into a sequence of instructions.
Returns an empty list if the method has no IL body. A body ending inside an opcode or operand
returns its valid prefix followed by one [IsMalformed](/api/dotsider.core.analysis.models.ilinstruction.ismalformed/) marker.

**Parameters:**

- `method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method to disassemble.

**Returns:** [IReadOnlyList\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

The list of decoded IL instructions.

```csharp
public IReadOnlyList<IlInstruction> Disassemble(MethodDefInfo method)
```

### DisassembleWithText(MethodDefInfo)

Disassembles a method and returns the text, instruction list, and header line count. A
malformed terminal opcode or operand is represented by one [IsMalformed](/api/dotsider.core.analysis.models.ilinstruction.ismalformed/) marker.

**Parameters:**

- `method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method to disassemble.

**Returns:** [Nullable\<String, IlInstruction\>, Int32\>\>](https://learn.microsoft.com/dotnet/api/system.nullable-3)

Tuple of (text, instructions, headerLineCount), or null if no IL body.

```csharp
public (string Text, IReadOnlyList<IlInstruction> Instructions, int HeaderLineCount)? DisassembleWithText(MethodDefInfo method)
```

### FormatDisassembly(MethodDefInfo)

Formats a complete disassembly listing for a method, including header information and any
terminal malformed-IL marker.

**Parameters:**

- `method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method to disassemble.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

A multi-line string with the full disassembly listing.

```csharp
public string FormatDisassembly(MethodDefInfo method)
```

### GetHeaderLineCount(MethodDefInfo)

Returns the number of header lines for a method's disassembly listing.

**Parameters:**

- `method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method to compute header lines for.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

The number of header lines, or 0 if no IL body.

```csharp
public int GetHeaderLineCount(MethodDefInfo method)
```
