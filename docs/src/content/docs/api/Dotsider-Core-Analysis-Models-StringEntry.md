---
title: "StringEntry"
description: "A string extracted from the assembly, along with its source and offset."
slug: api/dotsider.core.analysis.models.stringentry
sidebar:
  order: 1
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

