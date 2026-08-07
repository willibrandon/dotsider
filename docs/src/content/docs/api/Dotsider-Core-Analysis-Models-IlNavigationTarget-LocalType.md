---
title: "IlNavigationTarget.LocalType"
description: "A type defined in the current assembly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.localtype
sidebar:
  order: 2
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

- `Type` ([TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/))

```csharp
public LocalType(TypeDefInfo Type)
```

## Properties

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

### Type

**Returns:** [TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/)

```csharp
public TypeDefInfo Type { get; init; }
```

## Methods

### Deconstruct(out TypeDefInfo)

**Parameters:**

- `Type` ([TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/))

```csharp
public void Deconstruct(out TypeDefInfo Type)
```

### Equals(IlNavigationTarget?)

**Parameters:**

- `other` ([IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override sealed bool Equals(IlNavigationTarget? other)
```

### Equals(LocalType?)

**Parameters:**

- `other` ([LocalType](/api/dotsider.core.analysis.models.ilnavigationtarget.localtype/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlNavigationTarget.LocalType? other)
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

### operator !=(LocalType?, LocalType?)

**Parameters:**

- `left` ([LocalType](/api/dotsider.core.analysis.models.ilnavigationtarget.localtype/))
- `right` ([LocalType](/api/dotsider.core.analysis.models.ilnavigationtarget.localtype/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlNavigationTarget.LocalType? left, IlNavigationTarget.LocalType? right)
```

### operator ==(LocalType?, LocalType?)

**Parameters:**

- `left` ([LocalType](/api/dotsider.core.analysis.models.ilnavigationtarget.localtype/))
- `right` ([LocalType](/api/dotsider.core.analysis.models.ilnavigationtarget.localtype/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlNavigationTarget.LocalType? left, IlNavigationTarget.LocalType? right)
```
