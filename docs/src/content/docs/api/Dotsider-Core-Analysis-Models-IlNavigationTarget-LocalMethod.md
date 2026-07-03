---
title: "IlNavigationTarget.LocalMethod"
description: "A method defined in the current assembly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.localmethod
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A method defined in the current assembly.

```csharp
public sealed record IlNavigationTarget.LocalMethod : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.LocalMethod>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.LocalMethod**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<LocalMethod\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### LocalMethod(MethodDefInfo)

A method defined in the current assembly.

**Parameters:**

- `Method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): 

```csharp
public LocalMethod(MethodDefInfo Method)
```

## Properties

### Method

**Returns:** [MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)

```csharp
public MethodDefInfo Method { get; init; }
```

