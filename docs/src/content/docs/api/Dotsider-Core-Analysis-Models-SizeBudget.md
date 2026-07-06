---
title: "SizeBudget"
description: "One size budget: a scope plus at least one limit. Parsed from the spec grammar ([scope:]limit(,limit)* — for example total:max=25mb,growth=1% or ns=System.Text.Json:growth=10kb) by SizeBudgetParser, or from a budget file's object form which can also carry a name, description, severity, and per-budget contributor count."
slug: api/dotsider.core.analysis.models.sizebudget
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One size budget: a scope plus at least one limit. Parsed from the spec grammar
(`[scope:]limit(,limit)*` — for example `total:max=25mb,growth=1%` or
`ns=System.Text.Json:growth=10kb`) by
[SizeBudgetParser](/api/dotsider.core.analysis.sizebudgetparser/), or from a budget file's object form
which can also carry a name, description, severity, and per-budget contributor count.

```csharp
public sealed record SizeBudget : IEquatable<SizeBudget>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeBudget**

## Implements

- [IEquatable\<SizeBudget\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeBudget(SizeBudgetScope, string?, long?, long?, double?, SizeBudgetSeverity, string?, string?, int?)

One size budget: a scope plus at least one limit. Parsed from the spec grammar
(`[scope:]limit(,limit)*` — for example `total:max=25mb,growth=1%` or
`ns=System.Text.Json:growth=10kb`) by
[SizeBudgetParser](/api/dotsider.core.analysis.sizebudgetparser/), or from a budget file's object form
which can also carry a name, description, severity, and per-budget contributor count.

**Parameters:**

- `Scope` ([SizeBudgetScope](/api/dotsider.core.analysis.models.sizebudgetscope/)): What the budget measures.
- `Target` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The namespace prefix or assembly simple name, or null for [Total](/api/dotsider.core.analysis.models.sizebudgetscope.total/).
- `MaxBytes` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The absolute cap on the current value in bytes, or null when not limited.
- `MaxGrowthBytes` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The cap on growth versus the baseline in bytes, or null when not limited.
- `MaxGrowthPercent` ([Nullable\<Double\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The cap on growth versus the baseline as a percentage, or null when not limited.
- `Severity` ([SizeBudgetSeverity](/api/dotsider.core.analysis.models.sizebudgetseverity/)): Whether a breach fails the check or only warns.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A stable display name for reports, or null to render the spec itself.
- `Description` ([String](https://learn.microsoft.com/dotnet/api/system.string)): An explanation shown alongside a breach, or null.
- `TopN` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): A per-budget override for how many contributors a breach lists, or null for the caller's default.

```csharp
public SizeBudget(SizeBudgetScope Scope, string? Target, long? MaxBytes, long? MaxGrowthBytes, double? MaxGrowthPercent, SizeBudgetSeverity Severity = SizeBudgetSeverity.Error, string? Name = null, string? Description = null, int? TopN = null)
```

## Properties

### Description

An explanation shown alongside a breach, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Description { get; init; }
```

### MaxBytes

The absolute cap on the current value in bytes, or null when not limited.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? MaxBytes { get; init; }
```

### MaxGrowthBytes

The cap on growth versus the baseline in bytes, or null when not limited.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? MaxGrowthBytes { get; init; }
```

### MaxGrowthPercent

The cap on growth versus the baseline as a percentage, or null when not limited.

**Returns:** [Nullable\<Double\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public double? MaxGrowthPercent { get; init; }
```

### Name

A stable display name for reports, or null to render the spec itself.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Name { get; init; }
```

### Scope

What the budget measures.

**Returns:** [SizeBudgetScope](/api/dotsider.core.analysis.models.sizebudgetscope/)

```csharp
public SizeBudgetScope Scope { get; init; }
```

### Severity

Whether a breach fails the check or only warns.

**Returns:** [SizeBudgetSeverity](/api/dotsider.core.analysis.models.sizebudgetseverity/)

```csharp
public SizeBudgetSeverity Severity { get; init; }
```

### Target

The namespace prefix or assembly simple name, or null for [Total](/api/dotsider.core.analysis.models.sizebudgetscope.total/).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Target { get; init; }
```

### TopN

A per-budget override for how many contributors a breach lists, or null for the caller's default.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? TopN { get; init; }
```

## Methods

### ToString()

Renders the budget back into spec-grammar form, for display when it has no [Name](/api/dotsider.core.analysis.models.sizebudget.name/).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

The spec string.

```csharp
public override string ToString()
```

