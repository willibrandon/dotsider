---
title: "IlNavigationTarget.ExternalType"
description: "A type in an external (referenced) assembly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.externaltype
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A type in an external (referenced) assembly.

```csharp
public sealed record IlNavigationTarget.ExternalType : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.ExternalType>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.ExternalType**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<ExternalType\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ExternalType(TypeRefInfo, string)

A type in an external (referenced) assembly.

**Parameters:**

- `TypeRef` ([TypeRefInfo](/api/dotsider.core.analysis.models.typerefinfo/)): 
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

```csharp
public ExternalType(TypeRefInfo TypeRef, string AssemblyName)
```

## Properties

### AssemblyName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### TypeRef

**Returns:** [TypeRefInfo](/api/dotsider.core.analysis.models.typerefinfo/)

```csharp
public TypeRefInfo TypeRef { get; init; }
```

