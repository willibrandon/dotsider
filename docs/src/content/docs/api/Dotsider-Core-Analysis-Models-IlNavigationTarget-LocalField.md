---
title: "IlNavigationTarget.LocalField"
description: "A field defined in the current assembly."
slug: api/dotsider.core.analysis.models.ilnavigationtarget.localfield
sidebar:
  order: 2
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

- `Field` ([FieldDefInfo](/api/dotsider.core.analysis.models.fielddefinfo/))
- `DeclaringType` ([TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/))

```csharp
public LocalField(FieldDefInfo Field, TypeDefInfo DeclaringType)
```

## Properties

### DeclaringType

**Returns:** [TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/)

```csharp
public TypeDefInfo DeclaringType { get; init; }
```

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

### Field

**Returns:** [FieldDefInfo](/api/dotsider.core.analysis.models.fielddefinfo/)

```csharp
public FieldDefInfo Field { get; init; }
```

## Methods

### Deconstruct(out FieldDefInfo, out TypeDefInfo)

**Parameters:**

- `Field` ([FieldDefInfo](/api/dotsider.core.analysis.models.fielddefinfo/))
- `DeclaringType` ([TypeDefInfo](/api/dotsider.core.analysis.models.typedefinfo/))

```csharp
public void Deconstruct(out FieldDefInfo Field, out TypeDefInfo DeclaringType)
```

### Equals(IlNavigationTarget?)

**Parameters:**

- `other` ([IlNavigationTarget](/api/dotsider.core.analysis.models.ilnavigationtarget/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override sealed bool Equals(IlNavigationTarget? other)
```

### Equals(LocalField?)

**Parameters:**

- `other` ([LocalField](/api/dotsider.core.analysis.models.ilnavigationtarget.localfield/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlNavigationTarget.LocalField? other)
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

### operator !=(LocalField?, LocalField?)

**Parameters:**

- `left` ([LocalField](/api/dotsider.core.analysis.models.ilnavigationtarget.localfield/))
- `right` ([LocalField](/api/dotsider.core.analysis.models.ilnavigationtarget.localfield/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlNavigationTarget.LocalField? left, IlNavigationTarget.LocalField? right)
```

### operator ==(LocalField?, LocalField?)

**Parameters:**

- `left` ([LocalField](/api/dotsider.core.analysis.models.ilnavigationtarget.localfield/))
- `right` ([LocalField](/api/dotsider.core.analysis.models.ilnavigationtarget.localfield/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlNavigationTarget.LocalField? left, IlNavigationTarget.LocalField? right)
```
