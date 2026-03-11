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

- `TotalEvents` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `EventsByCategory` ([IReadOnlyDictionary\<TraceEventCategory, Int32\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)): 
- `Duration` ([TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)): 
- `PeakWorkingSetMb` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 
- `PeakGcHeapMb` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 
- `TotalExceptions` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 
- `TotalGcCollections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 
- `JittedMethodCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 

```csharp
public TraceSummary(int TotalEvents, IReadOnlyDictionary<TraceEventCategory, int> EventsByCategory, TimeSpan Duration, double PeakWorkingSetMb, double PeakGcHeapMb, long TotalExceptions, long TotalGcCollections, int JittedMethodCount)
```

## Properties

### Duration

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

```csharp
public TimeSpan Duration { get; init; }
```

### EventsByCategory

**Returns:** [IReadOnlyDictionary\<TraceEventCategory, Int32\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

```csharp
public IReadOnlyDictionary<TraceEventCategory, int> EventsByCategory { get; init; }
```

### JittedMethodCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int JittedMethodCount { get; init; }
```

### PeakGcHeapMb

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double PeakGcHeapMb { get; init; }
```

### PeakWorkingSetMb

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double PeakWorkingSetMb { get; init; }
```

### TotalEvents

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TotalEvents { get; init; }
```

### TotalExceptions

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long TotalExceptions { get; init; }
```

### TotalGcCollections

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long TotalGcCollections { get; init; }
```

