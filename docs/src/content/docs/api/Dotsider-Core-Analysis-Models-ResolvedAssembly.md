---
title: "ResolvedAssembly"
description: "The result of resolving an assembly reference to an assembly file, a bundle entry, or an authenticated sibling module."
slug: api/dotsider.core.analysis.models.resolvedassembly
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The result of resolving an assembly reference to an assembly file, a bundle entry, or an
authenticated sibling module.

```csharp
public abstract record ResolvedAssembly : IEquatable<ResolvedAssembly>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ResolvedAssembly**

## Implements

- [IEquatable\<ResolvedAssembly\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ResolvedAssembly()

```csharp
protected ResolvedAssembly()
```

### ResolvedAssembly(ResolvedAssembly)

**Parameters:**

- `original` ([ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/))

```csharp
protected ResolvedAssembly(ResolvedAssembly original)
```

## Properties

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected virtual Type EqualityContract { get; }
```

## Methods

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(ResolvedAssembly?)

**Parameters:**

- `other` ([ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public virtual bool Equals(ResolvedAssembly? other)
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

### operator !=(ResolvedAssembly?, ResolvedAssembly?)

**Parameters:**

- `left` ([ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/))
- `right` ([ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ResolvedAssembly? left, ResolvedAssembly? right)
```

### operator ==(ResolvedAssembly?, ResolvedAssembly?)

**Parameters:**

- `left` ([ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/))
- `right` ([ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ResolvedAssembly? left, ResolvedAssembly? right)
```
