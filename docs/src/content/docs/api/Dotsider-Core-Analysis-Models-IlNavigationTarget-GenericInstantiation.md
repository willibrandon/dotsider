---
title: "IlNavigationTarget.GenericInstantiation"
description: "A generic instantiation (TypeSpec or MethodSpec) that cannot be navigated directly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.genericinstantiation
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A generic instantiation (TypeSpec or MethodSpec) that cannot be navigated directly.

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

A generic instantiation (TypeSpec or MethodSpec) that cannot be navigated directly.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `DisplayName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

```csharp
public GenericInstantiation(int Token, string DisplayName)
```

## Properties

### DisplayName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DisplayName { get; init; }
```

### Token

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Token { get; init; }
```

