---
title: "IlNavigationTarget.Unsupported"
description: "A token kind that is recognized but not supported for navigation."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.unsupported
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A token kind that is recognized but not supported for navigation.

```csharp
public sealed record IlNavigationTarget.Unsupported : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.Unsupported>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.Unsupported**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<Unsupported\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### Unsupported(int, string)

A token kind that is recognized but not supported for navigation.

**Parameters:**

- `Token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Reason` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public Unsupported(int Token, string Reason)
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

### Equals(Unsupported?)

**Parameters:**

- `other` ([Unsupported](/api/dotsider.core.analysis.models.ilnavigationtarget.unsupported/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlNavigationTarget.Unsupported? other)
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

### operator !=(Unsupported?, Unsupported?)

**Parameters:**

- `left` ([Unsupported](/api/dotsider.core.analysis.models.ilnavigationtarget.unsupported/))
- `right` ([Unsupported](/api/dotsider.core.analysis.models.ilnavigationtarget.unsupported/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlNavigationTarget.Unsupported? left, IlNavigationTarget.Unsupported? right)
```

### operator ==(Unsupported?, Unsupported?)

**Parameters:**

- `left` ([Unsupported](/api/dotsider.core.analysis.models.ilnavigationtarget.unsupported/))
- `right` ([Unsupported](/api/dotsider.core.analysis.models.ilnavigationtarget.unsupported/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlNavigationTarget.Unsupported? left, IlNavigationTarget.Unsupported? right)
```
