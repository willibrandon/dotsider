---
title: "LocalSlotInfo"
description: "A PDB local variable slot and the IL range where its name is active."
slug: api/dotsider.core.analysis.models.localslotinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A PDB local variable slot and the IL range where its name is active.

```csharp
public sealed record LocalSlotInfo : IEquatable<LocalSlotInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **LocalSlotInfo**

## Implements

- [IEquatable\<LocalSlotInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### LocalSlotInfo(int, string, int, int, bool)

A PDB local variable slot and the IL range where its name is active.

**Parameters:**

- `Slot` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The local variable slot index.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The local variable name.
- `StartOffset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The first IL offset where the name is active.
- `EndOffset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The exclusive end IL offset for the local scope.
- `IsDebuggerHidden` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the local is marked debugger-hidden.

```csharp
public LocalSlotInfo(int Slot, string Name, int StartOffset, int EndOffset, bool IsDebuggerHidden)
```

## Properties

### EndOffset

The exclusive end IL offset for the local scope.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int EndOffset { get; init; }
```

### IsDebuggerHidden

Whether the local is marked debugger-hidden.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsDebuggerHidden { get; init; }
```

### Name

The local variable name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Slot

The local variable slot index.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Slot { get; init; }
```

### StartOffset

The first IL offset where the name is active.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int StartOffset { get; init; }
```

## Methods

### Deconstruct(out int, out string, out int, out int, out bool)

**Parameters:**

- `Slot` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `StartOffset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `EndOffset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `IsDebuggerHidden` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))

```csharp
public void Deconstruct(out int Slot, out string Name, out int StartOffset, out int EndOffset, out bool IsDebuggerHidden)
```

### Equals(LocalSlotInfo?)

**Parameters:**

- `other` ([LocalSlotInfo](/api/dotsider.core.analysis.models.localslotinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(LocalSlotInfo? other)
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

### operator !=(LocalSlotInfo?, LocalSlotInfo?)

**Parameters:**

- `left` ([LocalSlotInfo](/api/dotsider.core.analysis.models.localslotinfo/))
- `right` ([LocalSlotInfo](/api/dotsider.core.analysis.models.localslotinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(LocalSlotInfo? left, LocalSlotInfo? right)
```

### operator ==(LocalSlotInfo?, LocalSlotInfo?)

**Parameters:**

- `left` ([LocalSlotInfo](/api/dotsider.core.analysis.models.localslotinfo/))
- `right` ([LocalSlotInfo](/api/dotsider.core.analysis.models.localslotinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(LocalSlotInfo? left, LocalSlotInfo? right)
```
