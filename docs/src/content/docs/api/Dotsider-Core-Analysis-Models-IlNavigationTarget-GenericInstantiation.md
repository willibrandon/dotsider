---
title: "IlNavigationTarget.GenericInstantiation"
description: "A MethodSpec whose metadata could not be decoded into a navigable target."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.genericinstantiation
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A MethodSpec whose metadata could not be decoded into a navigable target.

```csharp
public sealed record IlNavigationTarget.GenericInstantiation : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.GenericInstantiation>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.GenericInstantiation**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<GenericInstantiation\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### GenericInstantiation(int, string)

A MethodSpec whose metadata could not be decoded into a navigable target.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `Reason` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

```csharp
public GenericInstantiation(int Token, string Reason)
```

## Properties

### Reason

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Reason { get; init; }
```

### Token

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

