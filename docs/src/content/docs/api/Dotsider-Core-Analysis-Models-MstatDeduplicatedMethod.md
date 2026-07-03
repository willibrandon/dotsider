---
title: "MstatDeduplicatedMethod"
description: "One method-body fold from an ILC size report (format 2.2+): the compiler emitted a single body and pointed these identical methods at it, so only the original contributes size."
slug: api/dotsider.core.analysis.models.mstatdeduplicatedmethod
sidebar:
  order: 1
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

