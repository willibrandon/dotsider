---
title: "IlNavigationTarget.Unresolved"
description: "A token that could not be resolved to any known target."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.unresolved
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A token that could not be resolved to any known target.

```csharp
public sealed record IlNavigationTarget.Unresolved : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.Unresolved>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.Unresolved**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<Unresolved\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### Unresolved(int, string)

A token that could not be resolved to any known target.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `Reason` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

```csharp
public Unresolved(int Token, string Reason)
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

