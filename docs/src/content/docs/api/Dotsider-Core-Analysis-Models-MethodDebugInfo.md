---
title: "MethodDebugInfo"
description: "Portable PDB debug information for a method."
slug: api/dotsider.core.analysis.models.methoddebuginfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Portable PDB debug information for a method.

```csharp
public sealed record MethodDebugInfo : IEquatable<MethodDebugInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MethodDebugInfo**

## Implements

- [IEquatable\<MethodDebugInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MethodDebugInfo(int, PdbProvenance, IReadOnlyList\<SequencePointInfo\>, IReadOnlyList\<LocalSlotInfo\>)

Portable PDB debug information for a method.

**Parameters:**

- `MethodToken` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The method definition metadata token.
- `Pdb` ([PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/)): The portable PDB provenance.
- `SequencePoints` ([IReadOnlyList\<SequencePointInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Decoded sequence points for the method.
- `Locals` ([IReadOnlyList\<LocalSlotInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Decoded local slots and PDB names for the method.

```csharp
public MethodDebugInfo(int MethodToken, PdbProvenance Pdb, IReadOnlyList<SequencePointInfo> SequencePoints, IReadOnlyList<LocalSlotInfo> Locals)
```

## Properties

### Locals

Decoded local slots and PDB names for the method.

**Returns:** [IReadOnlyList\<LocalSlotInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<LocalSlotInfo> Locals { get; init; }
```

### MethodToken

The method definition metadata token.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodToken { get; init; }
```

### Pdb

The portable PDB provenance.

**Returns:** [PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/)

```csharp
public PdbProvenance Pdb { get; init; }
```

### SequencePoints

Decoded sequence points for the method.

**Returns:** [IReadOnlyList\<SequencePointInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SequencePointInfo> SequencePoints { get; init; }
```

## Methods

### Deconstruct(out int, out PdbProvenance, out IReadOnlyList\<SequencePointInfo\>, out IReadOnlyList\<LocalSlotInfo\>)

**Parameters:**

- `MethodToken` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Pdb` ([PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/))
- `SequencePoints` ([IReadOnlyList\<SequencePointInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Locals` ([IReadOnlyList\<LocalSlotInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out int MethodToken, out PdbProvenance Pdb, out IReadOnlyList<SequencePointInfo> SequencePoints, out IReadOnlyList<LocalSlotInfo> Locals)
```

### Equals(MethodDebugInfo?)

**Parameters:**

- `other` ([MethodDebugInfo](/api/dotsider.core.analysis.models.methoddebuginfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MethodDebugInfo? other)
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

### operator !=(MethodDebugInfo?, MethodDebugInfo?)

**Parameters:**

- `left` ([MethodDebugInfo](/api/dotsider.core.analysis.models.methoddebuginfo/))
- `right` ([MethodDebugInfo](/api/dotsider.core.analysis.models.methoddebuginfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MethodDebugInfo? left, MethodDebugInfo? right)
```

### operator ==(MethodDebugInfo?, MethodDebugInfo?)

**Parameters:**

- `left` ([MethodDebugInfo](/api/dotsider.core.analysis.models.methoddebuginfo/))
- `right` ([MethodDebugInfo](/api/dotsider.core.analysis.models.methoddebuginfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MethodDebugInfo? left, MethodDebugInfo? right)
```
