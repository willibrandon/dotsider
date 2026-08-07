---
title: "CorrelationQueryResult"
description: "The result of a CorrelationQuery: an Outcome with exactly the payload that outcome carries — a Report when resolved, a candidate list when ambiguous, a Message explaining a miss or an unavailable index."
slug: api/dotsider.core.analysis.models.correlationqueryresult
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The result of a [CorrelationQuery](/api/dotsider.core.analysis.correlationquery/): an [Outcome](/api/dotsider.core.analysis.models.correlationqueryresult.outcome/) with exactly the
payload that outcome carries — a [Report](/api/dotsider.core.analysis.models.correlationqueryresult.report/) when resolved, a candidate list when
ambiguous, a [Message](/api/dotsider.core.analysis.models.correlationqueryresult.message/) explaining a miss or an unavailable index.

```csharp
public sealed record CorrelationQueryResult : IEquatable<CorrelationQueryResult>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **CorrelationQueryResult**

## Implements

- [IEquatable\<CorrelationQueryResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### CorrelationQueryResult(CorrelationQueryOutcome, CorrelationReport?, IReadOnlyList\<CorrelationCandidate\>, string?)

The result of a [CorrelationQuery](/api/dotsider.core.analysis.correlationquery/): an [Outcome](/api/dotsider.core.analysis.models.correlationqueryresult.outcome/) with exactly the
payload that outcome carries — a [Report](/api/dotsider.core.analysis.models.correlationqueryresult.report/) when resolved, a candidate list when
ambiguous, a [Message](/api/dotsider.core.analysis.models.correlationqueryresult.message/) explaining a miss or an unavailable index.

**Parameters:**

- `Outcome` ([CorrelationQueryOutcome](/api/dotsider.core.analysis.models.correlationqueryoutcome/)): How the query resolved.
- `Report` ([CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/)): The resolved correlation, or null unless [Outcome](/api/dotsider.core.analysis.models.correlationqueryresult.outcome/) is [Resolved](/api/dotsider.core.analysis.models.correlationqueryoutcome.resolved/).
- `Candidates` ([IReadOnlyList\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The ambiguous matches, empty unless [Outcome](/api/dotsider.core.analysis.models.correlationqueryresult.outcome/) is [Ambiguous](/api/dotsider.core.analysis.models.correlationqueryoutcome.ambiguous/).
- `Message` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A human-readable explanation for a non-resolved outcome, or null when resolved.

```csharp
public CorrelationQueryResult(CorrelationQueryOutcome Outcome, CorrelationReport? Report, IReadOnlyList<CorrelationCandidate> Candidates, string? Message)
```

## Properties

### Candidates

The ambiguous matches, empty unless [Outcome](/api/dotsider.core.analysis.models.correlationqueryresult.outcome/) is [Ambiguous](/api/dotsider.core.analysis.models.correlationqueryoutcome.ambiguous/).

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

**Returns:** [CorrelationQueryOutcome](/api/dotsider.core.analysis.models.correlationqueryoutcome/)

```csharp
public CorrelationQueryOutcome Outcome { get; init; }
```

### Report

The resolved correlation, or null unless [Outcome](/api/dotsider.core.analysis.models.correlationqueryresult.outcome/) is [Resolved](/api/dotsider.core.analysis.models.correlationqueryoutcome.resolved/).

**Returns:** [CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/)

```csharp
public CorrelationReport? Report { get; init; }
```

## Methods

### Ambiguous(IReadOnlyList\<CorrelationCandidate\>, string)

Creates an ambiguous result listing every matched candidate.

**Parameters:**

- `candidates` ([IReadOnlyList\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The matched candidates.
- `message` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A summary of the ambiguity.

**Returns:** [CorrelationQueryResult](/api/dotsider.core.analysis.models.correlationqueryresult/)

```csharp
public static CorrelationQueryResult Ambiguous(IReadOnlyList<CorrelationCandidate> candidates, string message)
```

### Deconstruct(out CorrelationQueryOutcome, out CorrelationReport?, out IReadOnlyList\<CorrelationCandidate\>, out string?)

**Parameters:**

- `Outcome` ([CorrelationQueryOutcome](/api/dotsider.core.analysis.models.correlationqueryoutcome/))
- `Report` ([CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/))
- `Candidates` ([IReadOnlyList\<CorrelationCandidate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Message` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out CorrelationQueryOutcome Outcome, out CorrelationReport? Report, out IReadOnlyList<CorrelationCandidate> Candidates, out string? Message)
```

### Equals(CorrelationQueryResult?)

**Parameters:**

- `other` ([CorrelationQueryResult](/api/dotsider.core.analysis.models.correlationqueryresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(CorrelationQueryResult? other)
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

### NotFound(string)

Creates a not-found result explaining the miss.

**Parameters:**

- `message` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Why nothing matched.

**Returns:** [CorrelationQueryResult](/api/dotsider.core.analysis.models.correlationqueryresult/)

```csharp
public static CorrelationQueryResult NotFound(string message)
```

### Resolved(CorrelationReport)

Creates a resolved result carrying the correlation report.

**Parameters:**

- `report` ([CorrelationReport](/api/dotsider.core.analysis.models.correlationreport/)): The resolved correlation payload.

**Returns:** [CorrelationQueryResult](/api/dotsider.core.analysis.models.correlationqueryresult/)

```csharp
public static CorrelationQueryResult Resolved(CorrelationReport report)
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

### Unavailable(string)

Creates an unavailable result explaining why correlation could not run.

**Parameters:**

- `message` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Why the index was unavailable.

**Returns:** [CorrelationQueryResult](/api/dotsider.core.analysis.models.correlationqueryresult/)

```csharp
public static CorrelationQueryResult Unavailable(string message)
```

## Members

### operator !=(CorrelationQueryResult?, CorrelationQueryResult?)

**Parameters:**

- `left` ([CorrelationQueryResult](/api/dotsider.core.analysis.models.correlationqueryresult/))
- `right` ([CorrelationQueryResult](/api/dotsider.core.analysis.models.correlationqueryresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(CorrelationQueryResult? left, CorrelationQueryResult? right)
```

### operator ==(CorrelationQueryResult?, CorrelationQueryResult?)

**Parameters:**

- `left` ([CorrelationQueryResult](/api/dotsider.core.analysis.models.correlationqueryresult/))
- `right` ([CorrelationQueryResult](/api/dotsider.core.analysis.models.correlationqueryresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(CorrelationQueryResult? left, CorrelationQueryResult? right)
```
