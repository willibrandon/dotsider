---
title: "TraceSummary"
description: "Summary statistics aggregated from all collected trace events."
slug: api/dotsider.core.analysis.models.tracesummary
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Summary statistics aggregated from all collected trace events.

```csharp
public sealed record TraceSummary : IEquatable<TraceSummary>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **TraceSummary**

## Implements

- [IEquatable\<TraceSummary\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### TraceSummary(int, IReadOnlyDictionary\<TraceEventCategory, int\>, TimeSpan, double, double, long, long, int)

Summary statistics aggregated from all collected trace events.

**Parameters:**

- `TotalEvents` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Total number of events captured during the trace.
- `EventsByCategory` ([IReadOnlyDictionary\<TraceEventCategory, Int32\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)): Event counts grouped by [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/).
- `Duration` ([TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)): Wall-clock duration of the trace session.
- `PeakWorkingSetMb` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): Peak process working set in megabytes.
- `PeakGcHeapMb` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): Peak GC heap size in megabytes.
- `TotalExceptions` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Total number of exceptions thrown during the trace.
- `TotalGcCollections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Total number of garbage collections across all generations.
- `JittedMethodCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of methods JIT-compiled during the trace.

```csharp
public TraceSummary(int TotalEvents, IReadOnlyDictionary<TraceEventCategory, int> EventsByCategory, TimeSpan Duration, double PeakWorkingSetMb, double PeakGcHeapMb, long TotalExceptions, long TotalGcCollections, int JittedMethodCount)
```

## Properties

### Duration

Wall-clock duration of the trace session.

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

```csharp
public TimeSpan Duration { get; init; }
```

### EventsByCategory

Event counts grouped by [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/).

**Returns:** [IReadOnlyDictionary\<TraceEventCategory, Int32\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

```csharp
public IReadOnlyDictionary<TraceEventCategory, int> EventsByCategory { get; init; }
```

### JittedMethodCount

Number of methods JIT-compiled during the trace.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int JittedMethodCount { get; init; }
```

### PeakGcHeapMb

Peak GC heap size in megabytes.

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double PeakGcHeapMb { get; init; }
```

### PeakWorkingSetMb

Peak process working set in megabytes.

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double PeakWorkingSetMb { get; init; }
```

### TotalEvents

Total number of events captured during the trace.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TotalEvents { get; init; }
```

### TotalExceptions

Total number of exceptions thrown during the trace.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long TotalExceptions { get; init; }
```

### TotalGcCollections

Total number of garbage collections across all generations.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long TotalGcCollections { get; init; }
```

