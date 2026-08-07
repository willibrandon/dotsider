---
title: "IlNavigationTarget.LocalMethod"
description: "A method defined in the current assembly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.localmethod
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A method defined in the current assembly.

```csharp
public sealed record IlNavigationTarget.LocalMethod : IlNavigationTarget, IEquatable<IlNavigationTarget>, IEquatable<IlNavigationTarget.LocalMethod>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → [IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/) → **IlNavigationTarget.LocalMethod**

## Implements

- [IEquatable\<IlNavigationTarget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)
- [IEquatable\<LocalMethod\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### LocalMethod(MethodDefInfo)

A method defined in the current assembly.

**Parameters:**

- `Method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/))

```csharp
public LocalMethod(MethodDefInfo Method)
```

## Properties

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

### Method

**Returns:** [MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)

```csharp
public MethodDefInfo Method { get; init; }
```

## Methods

### Deconstruct(out MethodDefInfo)

**Parameters:**

- `Method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/))

```csharp
public void Deconstruct(out MethodDefInfo Method)
```

### Equals(IlNavigationTarget?)

**Parameters:**

- `other` ([IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override sealed bool Equals(IlNavigationTarget? other)
```

### Equals(LocalMethod?)

**Parameters:**

- `other` ([LocalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.localmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlNavigationTarget.LocalMethod? other)
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

### operator !=(LocalMethod?, LocalMethod?)

**Parameters:**

- `left` ([LocalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.localmethod/))
- `right` ([LocalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.localmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlNavigationTarget.LocalMethod? left, IlNavigationTarget.LocalMethod? right)
```

### operator ==(LocalMethod?, LocalMethod?)

**Parameters:**

- `left` ([LocalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.localmethod/))
- `right` ([LocalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.localmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlNavigationTarget.LocalMethod? left, IlNavigationTarget.LocalMethod? right)
```
