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

