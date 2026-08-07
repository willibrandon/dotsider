---
title: "NativeSourceLine"
description: "One address→source mapping row recovered from a native sidecar (PDB C13 line table, DWARF/dSYM line program): the virtual address a source line begins at, its byte length, and the file and 1-based line number."
slug: api/dotsider.core.analysis.models.nativesourceline
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One address→source mapping row recovered from a native sidecar (PDB C13 line table, DWARF/dSYM
line program): the virtual address a source line begins at, its byte length, and the file and
1-based line number.

```csharp
public sealed record NativeSourceLine : IEquatable<NativeSourceLine>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeSourceLine**

## Implements

- [IEquatable\<NativeSourceLine\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeSourceLine(ulong, uint, string, int)

One address→source mapping row recovered from a native sidecar (PDB C13 line table, DWARF/dSYM
line program): the virtual address a source line begins at, its byte length, and the file and
1-based line number.

**Parameters:**

- `Address` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The virtual address the source line begins at.
- `Length` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): The number of bytes the row covers.
- `File` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The source file path.
- `Line` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The 1-based source line number.

```csharp
public NativeSourceLine(ulong Address, uint Length, string File, int Line)
```

## Properties

### Address

The virtual address the source line begins at.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong Address { get; init; }
```

### File

The source file path.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string File { get; init; }
```

### Length

The number of bytes the row covers.

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint Length { get; init; }
```

### Line

The 1-based source line number.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Line { get; init; }
```

## Methods

### Deconstruct(out ulong, out uint, out string, out int)

**Parameters:**

- `Address` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `Length` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32))
- `File` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Line` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out ulong Address, out uint Length, out string File, out int Line)
```

### Equals(NativeSourceLine?)

**Parameters:**

- `other` ([NativeSourceLine](/api/dotsider.core.analysis.models.nativesourceline/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeSourceLine? other)
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

### operator !=(NativeSourceLine?, NativeSourceLine?)

**Parameters:**

- `left` ([NativeSourceLine](/api/dotsider.core.analysis.models.nativesourceline/))
- `right` ([NativeSourceLine](/api/dotsider.core.analysis.models.nativesourceline/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeSourceLine? left, NativeSourceLine? right)
```

### operator ==(NativeSourceLine?, NativeSourceLine?)

**Parameters:**

- `left` ([NativeSourceLine](/api/dotsider.core.analysis.models.nativesourceline/))
- `right` ([NativeSourceLine](/api/dotsider.core.analysis.models.nativesourceline/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeSourceLine? left, NativeSourceLine? right)
```
