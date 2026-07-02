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

### RecoveredType(string, IReadOnlyList\<string\>)

A type recovered from a Native AOT binary's embedded NativeFormat metadata. ILC strips
ECMA-335 metadata, but the reflection and stack-trace metadata it keeps still names the
binary's own types and methods, so a stripped binary can describe itself.

**Parameters:**

- `FullName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The namespace-qualified type name (nested types use `+`).
- `MethodNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The names of the type's methods.

```csharp
public RecoveredType(string FullName, IReadOnlyList<string> MethodNames)
```

## Properties

### FullName

The namespace-qualified type name (nested types use `+`).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullName { get; init; }
```

### MethodNames

The names of the type's methods.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> MethodNames { get; init; }
```

