---
title: "IlDisassembler"
description: "Decodes IL (Intermediate Language) method bodies into human-readable instruction sequences."
slug: api/dotsider.core.analysis.ildisassembler
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

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): 

```csharp
public IlDisassembler(AssemblyAnalyzer analyzer)
```

## Methods

### Disassemble(MethodDefInfo)

Disassembles a method's IL body into a sequence of instructions.
Returns an empty list if the method has no IL body.

**Parameters:**

- `method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method to disassemble.

**Returns:** [IReadOnlyList\<IlInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

The list of decoded IL instructions.

```csharp
public IReadOnlyList<IlInstruction> Disassemble(MethodDefInfo method)
```

### FormatDisassembly(MethodDefInfo)

Formats a complete disassembly listing for a method, including header information.

**Parameters:**

- `method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method to disassemble.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

A multi-line string with the full disassembly listing.

```csharp
public string FormatDisassembly(MethodDefInfo method)
```

