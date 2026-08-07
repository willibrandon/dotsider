---
title: "IlNavigationTarget.GenericInstantiation"
description: "A MethodSpec whose metadata could not be decoded into a navigable target."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.genericinstantiation
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A MethodSpec whose metadata could not be decoded into a navigable target.

```csharp
public sealed record IlNavigationTarget.GenericInstantiation : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.GenericInstantiation>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.GenericInstantiation**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<GenericInstantiation\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### GenericInstantiation(int, string)

A MethodSpec whose metadata could not be decoded into a navigable target.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Reason` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public GenericInstantiation(int Token, string Reason)
```

## Properties

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

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

## Methods

### Deconstruct(out int, out string)

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Reason` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out int Token, out string Reason)
```

### Equals(GenericInstantiation?)

**Parameters:**

- `other` ([GenericInstantiation](/api/dotsider.core.analysis.models.ilnavigationtarget.genericinstantiation/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlNavigationTarget.GenericInstantiation? other)
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

### operator !=(GenericInstantiation?, GenericInstantiation?)

**Parameters:**

- `left` ([GenericInstantiation](/api/dotsider.core.analysis.models.ilnavigationtarget.genericinstantiation/))
- `right` ([GenericInstantiation](/api/dotsider.core.analysis.models.ilnavigationtarget.genericinstantiation/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlNavigationTarget.GenericInstantiation? left, IlNavigationTarget.GenericInstantiation? right)
```

### operator ==(GenericInstantiation?, GenericInstantiation?)

**Parameters:**

- `left` ([GenericInstantiation](/api/dotsider.core.analysis.models.ilnavigationtarget.genericinstantiation/))
- `right` ([GenericInstantiation](/api/dotsider.core.analysis.models.ilnavigationtarget.genericinstantiation/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlNavigationTarget.GenericInstantiation? left, IlNavigationTarget.GenericInstantiation? right)
```
