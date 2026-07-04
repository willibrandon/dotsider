---
title: "IlNavigationTarget.ExternalMethod"
description: "A method in an external (referenced) assembly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.externalmethod
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A method in an external (referenced) assembly.

```csharp
public sealed record IlNavigationTarget.ExternalMethod : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.ExternalMethod>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.ExternalMethod**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<ExternalMethod\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ExternalMethod(string, string, string, string)

A method in an external (referenced) assembly.

**Parameters:**

- `MemberName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

```csharp
public ExternalMethod(string MemberName, string DeclaringType, string Signature, string AssemblyName)
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

### MemberName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string MemberName { get; init; }
```

### Signature

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Signature { get; init; }
```

