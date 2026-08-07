---
title: "ManagedMethodSource"
description: "One managed assembly's contribution to a managed↔native correlation build: its simple name (ILC embeds it in every mangled symbol) and its method definitions."
slug: api/dotsider.core.analysis.models.managedmethodsource
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One managed assembly's contribution to a managed↔native correlation build: its simple
name (ILC embeds it in every mangled symbol) and its method definitions.

```csharp
public sealed record ManagedMethodSource : IEquatable<ManagedMethodSource>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ManagedMethodSource**

## Implements

- [IEquatable\<ManagedMethodSource\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ManagedMethodSource(string, IReadOnlyList\<MethodDefInfo\>)

One managed assembly's contribution to a managed↔native correlation build: its simple
name (ILC embeds it in every mangled symbol) and its method definitions.

**Parameters:**

- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly simple name, exactly as mstat records attribute it.
- `Methods` ([IReadOnlyList\<MethodDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The assembly's method definitions.

```csharp
public ManagedMethodSource(string AssemblyName, IReadOnlyList<MethodDefInfo> Methods)
```

## Properties

### AssemblyName

The assembly simple name, exactly as mstat records attribute it.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### Methods

The assembly's method definitions.

**Returns:** [IReadOnlyList\<MethodDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MethodDefInfo> Methods { get; init; }
```

## Methods

### Deconstruct(out string, out IReadOnlyList\<MethodDefInfo\>)

**Parameters:**

- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Methods` ([IReadOnlyList\<MethodDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string AssemblyName, out IReadOnlyList<MethodDefInfo> Methods)
```

### Equals(ManagedMethodSource?)

**Parameters:**

- `other` ([ManagedMethodSource](/api/dotsider.core.analysis.models.managedmethodsource/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ManagedMethodSource? other)
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

### operator !=(ManagedMethodSource?, ManagedMethodSource?)

**Parameters:**

- `left` ([ManagedMethodSource](/api/dotsider.core.analysis.models.managedmethodsource/))
- `right` ([ManagedMethodSource](/api/dotsider.core.analysis.models.managedmethodsource/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ManagedMethodSource? left, ManagedMethodSource? right)
```

### operator ==(ManagedMethodSource?, ManagedMethodSource?)

**Parameters:**

- `left` ([ManagedMethodSource](/api/dotsider.core.analysis.models.managedmethodsource/))
- `right` ([ManagedMethodSource](/api/dotsider.core.analysis.models.managedmethodsource/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ManagedMethodSource? left, ManagedMethodSource? right)
```
