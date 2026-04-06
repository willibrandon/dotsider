---
title: "IlNavigationTarget.Unsupported"
description: "A token kind that is recognized but not supported for navigation."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.unsupported
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A token kind that is recognized but not supported for navigation.

```csharp
public sealed record IlNavigationTarget.Unsupported : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.Unsupported>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.Unsupported**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<Unsupported\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### Unsupported(int, string)

A token kind that is recognized but not supported for navigation.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `Reason` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

```csharp
public Unsupported(int Token, string Reason)
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

