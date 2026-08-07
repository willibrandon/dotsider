---
title: "IlNavigationTarget.Unresolved"
description: "A token that could not be resolved to any known target."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.unresolved
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A token that could not be resolved to any known target.

```csharp
public sealed record IlNavigationTarget.Unresolved : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.Unresolved>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.Unresolved**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<Unresolved\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### Unresolved(int, string)

A token that could not be resolved to any known target.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Reason` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public Unresolved(int Token, string Reason)
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

### Equals(Unresolved?)

**Parameters:**

- `other` ([Unresolved](/api/dotsider.core.analysis.models.ilnavigationtarget.unresolved/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlNavigationTarget.Unresolved? other)
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

### operator !=(Unresolved?, Unresolved?)

**Parameters:**

- `left` ([Unresolved](/api/dotsider.core.analysis.models.ilnavigationtarget.unresolved/))
- `right` ([Unresolved](/api/dotsider.core.analysis.models.ilnavigationtarget.unresolved/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlNavigationTarget.Unresolved? left, IlNavigationTarget.Unresolved? right)
```

### operator ==(Unresolved?, Unresolved?)

**Parameters:**

- `left` ([Unresolved](/api/dotsider.core.analysis.models.ilnavigationtarget.unresolved/))
- `right` ([Unresolved](/api/dotsider.core.analysis.models.ilnavigationtarget.unresolved/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlNavigationTarget.Unresolved? left, IlNavigationTarget.Unresolved? right)
```
