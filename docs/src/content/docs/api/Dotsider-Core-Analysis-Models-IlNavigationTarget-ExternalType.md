---
title: "IlNavigationTarget.ExternalType"
description: "A type in an external (referenced) assembly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.externaltype
sidebar:
  order: 2
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

- `TypeRef` ([TypeRefInfo](/api/dotsider.core.analysis.models.typerefinfo/))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public ExternalType(TypeRefInfo TypeRef, string AssemblyName)
```

## Properties

### AssemblyName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

### TypeRef

**Returns:** [TypeRefInfo](/api/dotsider.core.analysis.models.typerefinfo/)

```csharp
public TypeRefInfo TypeRef { get; init; }
```

## Methods

### Deconstruct(out TypeRefInfo, out string)

**Parameters:**

- `TypeRef` ([TypeRefInfo](/api/dotsider.core.analysis.models.typerefinfo/))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out TypeRefInfo TypeRef, out string AssemblyName)
```

### Equals(ExternalType?)

**Parameters:**

- `other` ([ExternalType](/api/dotsider.core.analysis.models.ilnavigationtarget.externaltype/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlNavigationTarget.ExternalType? other)
```

### Equals(IlNavigationTarget?)

**Parameters:**

- `other` ([IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override sealed bool Equals(IlNavigationTarget? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### PrintMembers(StringBuilder)

**Parameters:**

- `builder` ([StringBuilder](https://learn.microsoft.com/dotnet/api/system.text.stringbuilder))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
protected override bool PrintMembers(StringBuilder builder)
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(ExternalType?, ExternalType?)

**Parameters:**

- `left` ([ExternalType](/api/dotsider.core.analysis.models.ilnavigationtarget.externaltype/))
- `right` ([ExternalType](/api/dotsider.core.analysis.models.ilnavigationtarget.externaltype/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlNavigationTarget.ExternalType? left, IlNavigationTarget.ExternalType? right)
```

### operator ==(ExternalType?, ExternalType?)

**Parameters:**

- `left` ([ExternalType](/api/dotsider.core.analysis.models.ilnavigationtarget.externaltype/))
- `right` ([ExternalType](/api/dotsider.core.analysis.models.ilnavigationtarget.externaltype/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlNavigationTarget.ExternalType? left, IlNavigationTarget.ExternalType? right)
```
