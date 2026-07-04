---
title: "IlNavigationTarget.ExternalField"
description: "A field in an external (referenced) assembly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.externalfield
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A field in an external (referenced) assembly.

```csharp
public sealed record IlNavigationTarget.ExternalField : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.ExternalField>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.ExternalField**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<ExternalField\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ExternalField(string, string, string)

A field in an external (referenced) assembly.

**Parameters:**

- `FieldName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

```csharp
public ExternalField(string FieldName, string DeclaringType, string AssemblyName)
```

## Properties

### AssemblyName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### DeclaringType

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DeclaringType { get; init; }
```

### FieldName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FieldName { get; init; }
```

