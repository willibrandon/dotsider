---
title: "CorrelationCandidate"
description: "One of several methods a name query matched. Overloads share a name, so an ambiguous query surfaces every candidate rather than guessing which the caller meant."
slug: api/dotsider.core.analysis.models.correlationcandidate
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One of several methods a name query matched. Overloads share a name, so an ambiguous
query surfaces every candidate rather than guessing which the caller meant.

```csharp
public sealed record CorrelationCandidate : IEquatable<CorrelationCandidate>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **CorrelationCandidate**

## Implements

- [IEquatable\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### CorrelationCandidate(string, string, string, int, ulong?)

One of several methods a name query matched. Overloads share a name, so an ambiguous
query surfaces every candidate rather than guessing which the caller meant.

**Parameters:**

- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly the method is defined in.
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The fully qualified declaring type name.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The method's simple name.
- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The method's metadata token.
- `VirtualAddress` ([Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The first correlated native address, or null when the method is not in the native image.

```csharp
public CorrelationCandidate(string AssemblyName, string DeclaringType, string Name, int Token, ulong? VirtualAddress)
```

## Properties

### AssemblyName

The simple name of the assembly the method is defined in.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### DeclaringType

The fully qualified declaring type name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DeclaringType { get; init; }
```

### Name

The method's simple name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Token

The method's metadata token.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

### VirtualAddress

The first correlated native address, or null when the method is not in the native image.

**Returns:** [Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public ulong? VirtualAddress { get; init; }
```

