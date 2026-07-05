---
title: "ReadyToRunDisassembler.MethodDisassembly"
description: "The result of disassembling a method across its ranges."
slug: api/dotsider.core.analysis.readytorun.readytorundisassembler.methoddisassembly
sidebar:
  order: 3
---

**Namespace:** `Dotsider.Core.Analysis.ReadyToRun`

**Assembly:** Dotsider.Core.dll

The result of disassembling a method across its ranges.

```csharp
public readonly record struct ReadyToRunDisassembler.MethodDisassembly : IEquatable<ReadyToRunDisassembler.MethodDisassembly>
```

## Implements

- [IEquatable\<MethodDisassembly\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MethodDisassembly(string, IReadOnlyList\<NativeInstruction\>)

The result of disassembling a method across its ranges.

**Parameters:**

- `Text` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The concatenated, block-separated disassembly text.
- `Instructions` ([IReadOnlyList\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Every decoded instruction across all ranges, in order.

```csharp
public MethodDisassembly(string Text, IReadOnlyList<NativeInstruction> Instructions)
```

## Properties

### Instructions

Every decoded instruction across all ranges, in order.

**Returns:** [IReadOnlyList\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NativeInstruction> Instructions { get; init; }
```

### Text

The concatenated, block-separated disassembly text.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Text { get; init; }
```

