---
title: "AssemblyOpenResult"
description: "The result of opening an assembly file via AssemblyLoader, distinguishing between direct loads, apphost companion redirects, and single-file bundle entry extractions."
slug: api/dotsider.core.analysis.models.assemblyopenresult
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The result of opening an assembly file via [AssemblyLoader](/api/dotsider.core.analysis.assemblyloader/),
distinguishing between direct loads, apphost companion redirects, and
single-file bundle entry extractions.

```csharp
public abstract record AssemblyOpenResult : IEquatable<AssemblyOpenResult>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyOpenResult**

## Implements

- [IEquatable\<AssemblyOpenResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### AssemblyOpenResult()

```csharp
protected AssemblyOpenResult()
```

### AssemblyOpenResult(AssemblyOpenResult)

**Parameters:**

- `original` ([AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/))

```csharp
protected AssemblyOpenResult(AssemblyOpenResult original)
```

## Properties

### EqualityContract

**Returns:** [Type](https://learn.microsoft.com/dotnet/api/system.type)

```csharp
protected virtual Type EqualityContract { get; }
```

## Methods

### Equals(AssemblyOpenResult?)

**Parameters:**

- `other` ([AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public virtual bool Equals(AssemblyOpenResult? other)
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

### operator !=(AssemblyOpenResult?, AssemblyOpenResult?)

**Parameters:**

- `left` ([AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/))
- `right` ([AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(AssemblyOpenResult? left, AssemblyOpenResult? right)
```

### operator ==(AssemblyOpenResult?, AssemblyOpenResult?)

**Parameters:**

- `left` ([AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/))
- `right` ([AssemblyOpenResult](/api/dotsider.core.analysis.models.assemblyopenresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(AssemblyOpenResult? left, AssemblyOpenResult? right)
```
