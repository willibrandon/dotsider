---
title: "MstatDeduplicatedMethod"
description: "One method-body fold from an ILC size report (format 2.2+): the compiler emitted a single body and pointed these identical methods at it, so only the original contributes size."
slug: api/dotsider.core.analysis.models.mstatdeduplicatedmethod
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One method-body fold from an ILC size report (format 2.2+): the compiler emitted a single
body and pointed these identical methods at it, so only the original contributes size.

```csharp
public sealed record MstatDeduplicatedMethod : IEquatable<MstatDeduplicatedMethod>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatDeduplicatedMethod**

## Implements

- [IEquatable\<MstatDeduplicatedMethod\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatDeduplicatedMethod(string, IReadOnlyList\<string\>)

One method-body fold from an ILC size report (format 2.2+): the compiler emitted a single
body and pointed these identical methods at it, so only the original contributes size.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The original method's display name, including its declaring type.
- `TargetNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The dependency-graph node names of the methods folded into the original.

```csharp
public MstatDeduplicatedMethod(string Name, IReadOnlyList<string> TargetNames)
```

## Properties

### Name

The original method's display name, including its declaring type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### TargetNames

The dependency-graph node names of the methods folded into the original.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> TargetNames { get; init; }
```

## Methods

### Deconstruct(out string, out IReadOnlyList\<string\>)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TargetNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string Name, out IReadOnlyList<string> TargetNames)
```

### Equals(MstatDeduplicatedMethod?)

**Parameters:**

- `other` ([MstatDeduplicatedMethod](/api/dotsider.core.analysis.models.mstatdeduplicatedmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatDeduplicatedMethod? other)
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

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(MstatDeduplicatedMethod?, MstatDeduplicatedMethod?)

**Parameters:**

- `left` ([MstatDeduplicatedMethod](/api/dotsider.core.analysis.models.mstatdeduplicatedmethod/))
- `right` ([MstatDeduplicatedMethod](/api/dotsider.core.analysis.models.mstatdeduplicatedmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatDeduplicatedMethod? left, MstatDeduplicatedMethod? right)
```

### operator ==(MstatDeduplicatedMethod?, MstatDeduplicatedMethod?)

**Parameters:**

- `left` ([MstatDeduplicatedMethod](/api/dotsider.core.analysis.models.mstatdeduplicatedmethod/))
- `right` ([MstatDeduplicatedMethod](/api/dotsider.core.analysis.models.mstatdeduplicatedmethod/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatDeduplicatedMethod? left, MstatDeduplicatedMethod? right)
```
