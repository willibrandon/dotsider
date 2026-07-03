---
title: "RecoveredType"
description: "A type recovered from a Native AOT binary's embedded NativeFormat metadata. ILC strips ECMA-335 metadata, but the reflection and stack-trace metadata it keeps still names the binary's own types and methods, so a stripped binary can describe itself."
slug: api/dotsider.core.analysis.models.recoveredtype
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A type recovered from a Native AOT binary's embedded NativeFormat metadata. ILC strips
ECMA-335 metadata, but the reflection and stack-trace metadata it keeps still names the
binary's own types and methods, so a stripped binary can describe itself.

```csharp
public sealed record RecoveredType : IEquatable<RecoveredType>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **RecoveredType**

## Implements

- [IEquatable\<RecoveredType\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### RecoveredType(string, IReadOnlyList\<string\>, string?)

A type recovered from a Native AOT binary's embedded NativeFormat metadata. ILC strips
ECMA-335 metadata, but the reflection and stack-trace metadata it keeps still names the
binary's own types and methods, so a stripped binary can describe itself.

**Parameters:**

- `FullName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The namespace-qualified type name (nested types use `+`).
- `MethodNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The names of the type's methods, in metadata order.
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The simple name of the assembly scope that defined the type, or null when the metadata
does not record one. Native symbol demangling joins mangled names against this scope.

```csharp
public RecoveredType(string FullName, IReadOnlyList<string> MethodNames, string? AssemblyName = null)
```

## Properties

### AssemblyName

The simple name of the assembly scope that defined the type, or null when the metadata
does not record one. Native symbol demangling joins mangled names against this scope.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyName { get; init; }
```

### FullName

The namespace-qualified type name (nested types use `+`).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullName { get; init; }
```

### MethodNames

The names of the type's methods, in metadata order.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> MethodNames { get; init; }
```

## Methods

### Deconstruct(out string, out IReadOnlyList\<string\>)

Deconstructs into the original two components, preserving call sites written before
[AssemblyName](/api/dotsider.core.analysis.models.recoveredtype.assemblyname/) existed — a record's generated Deconstruct grows with its
positional parameters, so the two-value form is kept explicitly.

**Parameters:**

- `fullName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The namespace-qualified type name.
- `methodNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The names of the type's methods, in metadata order.

```csharp
public void Deconstruct(out string fullName, out IReadOnlyList<string> methodNames)
```

