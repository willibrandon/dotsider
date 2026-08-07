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
public readonly struct ReadyToRunDisassembler.MethodDisassembly : IEquatable<ReadyToRunDisassembler.MethodDisassembly>
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

## Methods

### Deconstruct(out string, out IReadOnlyList\<NativeInstruction\>)

**Parameters:**

- `Text` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Instructions` ([IReadOnlyList\<NativeInstruction\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string Text, out IReadOnlyList<NativeInstruction> Instructions)
```

### Equals(MethodDisassembly)

**Parameters:**

- `other` ([MethodDisassembly](/api/dotsider.core.analysis.readytorun.readytorundisassembler.methoddisassembly/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ReadyToRunDisassembler.MethodDisassembly other)
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

### operator !=(MethodDisassembly, MethodDisassembly)

**Parameters:**

- `left` ([MethodDisassembly](/api/dotsider.core.analysis.readytorun.readytorundisassembler.methoddisassembly/))
- `right` ([MethodDisassembly](/api/dotsider.core.analysis.readytorun.readytorundisassembler.methoddisassembly/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ReadyToRunDisassembler.MethodDisassembly left, ReadyToRunDisassembler.MethodDisassembly right)
```

### operator ==(MethodDisassembly, MethodDisassembly)

**Parameters:**

- `left` ([MethodDisassembly](/api/dotsider.core.analysis.readytorun.readytorundisassembler.methoddisassembly/))
- `right` ([MethodDisassembly](/api/dotsider.core.analysis.readytorun.readytorundisassembler.methoddisassembly/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ReadyToRunDisassembler.MethodDisassembly left, ReadyToRunDisassembler.MethodDisassembly right)
```
