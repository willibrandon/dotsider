---
title: "IlNavigationTarget.LocalType"
description: "A type defined in the current assembly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.localtype
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A type defined in the current assembly.

```csharp
public sealed record IlNavigationTarget.LocalType : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.LocalType>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.LocalType**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<LocalType\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### LocalType(TypeDefInfo)

A type defined in the current assembly.

**Parameters:**

- `Type` ([TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/)): 

```csharp
public LocalType(TypeDefInfo Type)
```

## Properties

### Type

**Returns:** [TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/)

```csharp
public TypeDefInfo Type { get; init; }
```

