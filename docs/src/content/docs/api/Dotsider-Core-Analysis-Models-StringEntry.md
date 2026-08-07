---
title: "StringEntry"
description: "A string extracted from the assembly, along with its source and offset."
slug: api/dotsider.core.analysis.models.stringentry
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A string extracted from the assembly, along with its source and offset.

```csharp
public sealed record StringEntry : IEquatable<StringEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **StringEntry**

## Implements

- [IEquatable\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### StringEntry(int, string, StringSource)

A string extracted from the assembly, along with its source and offset.

**Parameters:**

- `Offset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The byte offset or heap handle where the string was found.
- `Value` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The string content.
- `Source` ([StringSource](/api/dotsider.core.analysis.models.stringsource/)): Which string source this entry came from.

```csharp
public StringEntry(int Offset, string Value, StringSource Source)
```

## Properties

### Offset

The byte offset or heap handle where the string was found.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Offset { get; init; }
```

### Source

Which string source this entry came from.

**Returns:** [StringSource](/api/dotsider.core.analysis.models.stringsource/)

```csharp
public StringSource Source { get; init; }
```

### Value

The string content.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Value { get; init; }
```

## Methods

### Deconstruct(out int, out string, out StringSource)

**Parameters:**

- `Offset` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Value` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Source` ([StringSource](/api/dotsider.core.analysis.models.stringsource/))

```csharp
public void Deconstruct(out int Offset, out string Value, out StringSource Source)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(StringEntry?)

**Parameters:**

- `other` ([StringEntry](/api/dotsider.core.analysis.models.stringentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(StringEntry? other)
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

### operator !=(StringEntry?, StringEntry?)

**Parameters:**

- `left` ([StringEntry](/api/dotsider.core.analysis.models.stringentry/))
- `right` ([StringEntry](/api/dotsider.core.analysis.models.stringentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(StringEntry? left, StringEntry? right)
```

### operator ==(StringEntry?, StringEntry?)

**Parameters:**

- `left` ([StringEntry](/api/dotsider.core.analysis.models.stringentry/))
- `right` ([StringEntry](/api/dotsider.core.analysis.models.stringentry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(StringEntry? left, StringEntry? right)
```
