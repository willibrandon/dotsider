---
title: "IlNavigationTarget.LocalField"
description: "A field defined in the current assembly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.localfield
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A field defined in the current assembly.

```csharp
public sealed record IlNavigationTarget.LocalField : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.LocalField>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.LocalField**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<LocalField\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### LocalField(FieldDefInfo, TypeDefInfo)

A field defined in the current assembly.

**Parameters:**

- `Field` ([FieldDefInfo](/api/dotsider.core.analysis.models.fielddefinfo/)): 
- `DeclaringType` ([TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/)): 

```csharp
public LocalField(FieldDefInfo Field, TypeDefInfo DeclaringType)
```

## Properties

### DeclaringType

**Returns:** [TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/)

```csharp
public TypeDefInfo DeclaringType { get; init; }
```

### Field

**Returns:** [FieldDefInfo](/api/dotsider.core.analysis.models.fielddefinfo/)

```csharp
public FieldDefInfo Field { get; init; }
```

