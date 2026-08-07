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

- `FieldName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))

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

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
```

### FieldName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FieldName { get; init; }
```

## Methods

### Deconstruct(out string, out string, out string)

**Parameters:**

- `FieldName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string FieldName, out string DeclaringType, out string AssemblyName)
```

### Equals(ExternalField?)

**Parameters:**

- `other` ([ExternalField](/api/dotsider.core.analysis.models.ilnavigationtarget.externalfield/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlNavigationTarget.ExternalField? other)
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

### operator !=(ExternalField?, ExternalField?)

**Parameters:**

- `left` ([ExternalField](/api/dotsider.core.analysis.models.ilnavigationtarget.externalfield/))
- `right` ([ExternalField](/api/dotsider.core.analysis.models.ilnavigationtarget.externalfield/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlNavigationTarget.ExternalField? left, IlNavigationTarget.ExternalField? right)
```

### operator ==(ExternalField?, ExternalField?)

**Parameters:**

- `left` ([ExternalField](/api/dotsider.core.analysis.models.ilnavigationtarget.externalfield/))
- `right` ([ExternalField](/api/dotsider.core.analysis.models.ilnavigationtarget.externalfield/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlNavigationTarget.ExternalField? left, IlNavigationTarget.ExternalField? right)
```
