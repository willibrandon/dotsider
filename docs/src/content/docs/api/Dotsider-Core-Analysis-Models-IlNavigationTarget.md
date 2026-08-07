---
title: "IlNavigationTarget"
description: "Represents the resolved target of an IL code navigation (go-to-definition) action."
slug: api/dotsider.core.analysis.models.ilnavigationtarget
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Represents the resolved target of an IL code navigation (go-to-definition) action.

```csharp
public abstract record IlNavigationTarget : IEquatable<IlNavigationTarget>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **IlNavigationTarget**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### IlNavigationTarget()

```csharp
protected IlNavigationTarget()
```

### IlNavigationTarget(IlNavigationTarget)

**Parameters:**

- `original` ([IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/))

```csharp
protected IlNavigationTarget(IlNavigationTarget original)
```

## Properties

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected virtual Type EqualityContract { get; }
```

## Methods

### Equals(IlNavigationTarget?)

**Parameters:**

- `other` ([IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public virtual bool Equals(IlNavigationTarget? other)
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
protected virtual bool PrintMembers(StringBuilder builder)
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(IlNavigationTarget?, IlNavigationTarget?)

**Parameters:**

- `left` ([IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/))
- `right` ([IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlNavigationTarget? left, IlNavigationTarget? right)
```

### operator ==(IlNavigationTarget?, IlNavigationTarget?)

**Parameters:**

- `left` ([IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/))
- `right` ([IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlNavigationTarget? left, IlNavigationTarget? right)
```
