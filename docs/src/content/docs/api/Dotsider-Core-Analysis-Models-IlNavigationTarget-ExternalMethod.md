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

- `MemberName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))

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

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected override Type EqualityContract { get; }
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

## Methods

### Deconstruct(out string, out string, out string, out string)

**Parameters:**

- `MemberName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `DeclaringType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Signature` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out string MemberName, out string DeclaringType, out string Signature, out string AssemblyName)
```

### Equals(ExternalMethod?)

**Parameters:**

- `other` ([ExternalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.externalmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(IlNavigationTarget.ExternalMethod? other)
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

### operator !=(ExternalMethod?, ExternalMethod?)

**Parameters:**

- `left` ([ExternalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.externalmethod/))
- `right` ([ExternalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.externalmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(IlNavigationTarget.ExternalMethod? left, IlNavigationTarget.ExternalMethod? right)
```

### operator ==(ExternalMethod?, ExternalMethod?)

**Parameters:**

- `left` ([ExternalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.externalmethod/))
- `right` ([ExternalMethod](/api/dotsider.core.analysis.models.ilnavigationtarget.externalmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(IlNavigationTarget.ExternalMethod? left, IlNavigationTarget.ExternalMethod? right)
```
