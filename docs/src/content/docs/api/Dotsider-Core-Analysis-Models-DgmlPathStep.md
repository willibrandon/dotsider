---
title: "DgmlPathStep"
description: "One step of a root-to-node dependency chain — the answer to \"why is this in my binary,\" read top-down: the root kept the second step, which kept the third, and so on to the node that was asked about."
slug: api/dotsider.core.analysis.models.dgmlpathstep
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One step of a root-to-node dependency chain — the answer to "why is this in my binary,"
read top-down: the root kept the second step, which kept the third, and so on to the node
that was asked about.

```csharp
public sealed record DgmlPathStep : IEquatable<DgmlPathStep>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DgmlPathStep**

## Implements

- [IEquatable\<DgmlPathStep\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### DgmlPathStep(string, string?)

One step of a root-to-node dependency chain — the answer to "why is this in my binary,"
read top-down: the root kept the second step, which kept the third, and so on to the node
that was asked about.

**Parameters:**

- `Label` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The node name at this step.
- `Reason` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Why the previous step depends on this one, or null on the root step.

```csharp
public DgmlPathStep(string Label, string? Reason)
```

## Properties

### Label

The node name at this step.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Label { get; init; }
```

### Reason

Why the previous step depends on this one, or null on the root step.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Reason { get; init; }
```

