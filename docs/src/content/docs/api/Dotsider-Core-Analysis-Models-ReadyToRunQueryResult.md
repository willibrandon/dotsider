---
title: "ReadyToRunQueryResult"
description: "The result of a ReadyToRunCorrelationQuery: an Outcome with exactly the payload that outcome carries — a Report when resolved, a candidate list when ambiguous, a Message explaining a miss or an unavailable image."
slug: api/dotsider.core.analysis.models.readytorunqueryresult
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The result of a [ReadyToRunCorrelationQuery](/api/dotsider.core.analysis.readytoruncorrelationquery/): an [Outcome](/api/dotsider.core.analysis.models.readytorunqueryresult.outcome/) with exactly
the payload that outcome carries — a [Report](/api/dotsider.core.analysis.models.readytorunqueryresult.report/) when resolved, a candidate list when
ambiguous, a [Message](/api/dotsider.core.analysis.models.readytorunqueryresult.message/) explaining a miss or an unavailable image.

```csharp
public sealed record ReadyToRunQueryResult : IEquatable<ReadyToRunQueryResult>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunQueryResult**

## Implements

- [IEquatable\<ReadyToRunQueryResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ReadyToRunQueryResult(ReadyToRunQueryOutcome, ReadyToRunMethodReport?, IReadOnlyList\<CorrelationCandidate\>, string?)

The result of a [ReadyToRunCorrelationQuery](/api/dotsider.core.analysis.readytoruncorrelationquery/): an [Outcome](/api/dotsider.core.analysis.models.readytorunqueryresult.outcome/) with exactly
the payload that outcome carries — a [Report](/api/dotsider.core.analysis.models.readytorunqueryresult.report/) when resolved, a candidate list when
ambiguous, a [Message](/api/dotsider.core.analysis.models.readytorunqueryresult.message/) explaining a miss or an unavailable image.

**Parameters:**

- `Outcome` ([ReadyToRunQueryOutcome](/api/dotsider.core.analysis.models.readytorunqueryoutcome/)): How the query resolved.
- `Report` ([ReadyToRunMethodReport](/api/dotsider.core.analysis.models.readytorunmethodreport/)): The resolved correlation, or null unless [Outcome](/api/dotsider.core.analysis.models.readytorunqueryresult.outcome/) is [Resolved](/api/dotsider.core.analysis.models.readytorunqueryoutcome.resolved/).
- `Candidates` ([IReadOnlyList\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The ambiguous matches, empty unless [Outcome](/api/dotsider.core.analysis.models.readytorunqueryresult.outcome/) is [Ambiguous](/api/dotsider.core.analysis.models.readytorunqueryoutcome.ambiguous/).
- `Message` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A human-readable explanation for a non-resolved outcome, or null when resolved.

```csharp
public ReadyToRunQueryResult(ReadyToRunQueryOutcome Outcome, ReadyToRunMethodReport? Report, IReadOnlyList<CorrelationCandidate> Candidates, string? Message)
```

## Properties

### Candidates

The ambiguous matches, empty unless [Outcome](/api/dotsider.core.analysis.models.readytorunqueryresult.outcome/) is [Ambiguous](/api/dotsider.core.analysis.models.readytorunqueryoutcome.ambiguous/).

**Returns:** [IReadOnlyList\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<CorrelationCandidate> Candidates { get; init; }
```

### Message

A human-readable explanation for a non-resolved outcome, or null when resolved.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Message { get; init; }
```

### Outcome

How the query resolved.

**Returns:** [ReadyToRunQueryOutcome](/api/dotsider.core.analysis.models.readytorunqueryoutcome/)

```csharp
public ReadyToRunQueryOutcome Outcome { get; init; }
```

### Report

The resolved correlation, or null unless [Outcome](/api/dotsider.core.analysis.models.readytorunqueryresult.outcome/) is [Resolved](/api/dotsider.core.analysis.models.readytorunqueryoutcome.resolved/).

**Returns:** [ReadyToRunMethodReport](/api/dotsider.core.analysis.models.readytorunmethodreport/)

```csharp
public ReadyToRunMethodReport? Report { get; init; }
```

## Methods

### Ambiguous(IReadOnlyList\<CorrelationCandidate\>, string)

Creates an ambiguous result listing every matched candidate.

**Parameters:**

- `candidates` ([IReadOnlyList\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The matched candidates.
- `message` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A summary of the ambiguity.

**Returns:** [ReadyToRunQueryResult](/api/dotsider.core.analysis.models.readytorunqueryresult/)

```csharp
public static ReadyToRunQueryResult Ambiguous(IReadOnlyList<CorrelationCandidate> candidates, string message)
```

### NotFound(string)

Creates a not-found result explaining the miss.

**Parameters:**

- `message` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Why nothing matched.

**Returns:** [ReadyToRunQueryResult](/api/dotsider.core.analysis.models.readytorunqueryresult/)

```csharp
public static ReadyToRunQueryResult NotFound(string message)
```

### Resolved(ReadyToRunMethodReport)

Creates a resolved result carrying the report.

**Parameters:**

- `report` ([ReadyToRunMethodReport](/api/dotsider.core.analysis.models.readytorunmethodreport/)): The resolved correlation payload.

**Returns:** [ReadyToRunQueryResult](/api/dotsider.core.analysis.models.readytorunqueryresult/)

```csharp
public static ReadyToRunQueryResult Resolved(ReadyToRunMethodReport report)
```

### Unavailable(string)

Creates an unavailable result explaining why correlation could not run.

**Parameters:**

- `message` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Why the image is not usable.

**Returns:** [ReadyToRunQueryResult](/api/dotsider.core.analysis.models.readytorunqueryresult/)

```csharp
public static ReadyToRunQueryResult Unavailable(string message)
```

