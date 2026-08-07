---
title: "CounterSnapshot"
description: "A snapshot of runtime performance counters at a point in time."
slug: api/dotsider.core.analysis.models.countersnapshot
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A snapshot of runtime performance counters at a point in time.

```csharp
public sealed record CounterSnapshot : IEquatable<CounterSnapshot>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **CounterSnapshot**

## Implements

- [IEquatable\<CounterSnapshot\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### CounterSnapshot(TimeSpan, double, double, double, long, long, long, int, long, long, long)

A snapshot of runtime performance counters at a point in time.

**Parameters:**

- `Timestamp` ([TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)): Elapsed time since the trace started.
- `CpuUsagePercent` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): CPU usage as a percentage (0–100).
- `WorkingSetMb` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): Process working set in megabytes.
- `GcHeapSizeMb` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): GC heap size in megabytes.
- `Gen0Collections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Cumulative generation 0 collection count.
- `Gen1Collections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Cumulative generation 1 collection count.
- `Gen2Collections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Cumulative generation 2 collection count.
- `ThreadPoolThreadCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Number of active thread pool threads.
- `ThreadPoolQueueLength` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Number of work items queued to the thread pool.
- `ExceptionCount` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Cumulative exception count.
- `ActiveTimerCount` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): Number of active timers.

```csharp
public CounterSnapshot(TimeSpan Timestamp, double CpuUsagePercent, double WorkingSetMb, double GcHeapSizeMb, long Gen0Collections, long Gen1Collections, long Gen2Collections, int ThreadPoolThreadCount, long ThreadPoolQueueLength, long ExceptionCount, long ActiveTimerCount)
```

## Properties

### ActiveTimerCount

Number of active timers.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long ActiveTimerCount { get; init; }
```

### CpuUsagePercent

CPU usage as a percentage (0–100).

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double CpuUsagePercent { get; init; }
```

### ExceptionCount

Cumulative exception count.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long ExceptionCount { get; init; }
```

### GcHeapSizeMb

GC heap size in megabytes.

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double GcHeapSizeMb { get; init; }
```

### Gen0Collections

Cumulative generation 0 collection count.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Gen0Collections { get; init; }
```

### Gen1Collections

Cumulative generation 1 collection count.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Gen1Collections { get; init; }
```

### Gen2Collections

Cumulative generation 2 collection count.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Gen2Collections { get; init; }
```

### ThreadPoolQueueLength

Number of work items queued to the thread pool.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long ThreadPoolQueueLength { get; init; }
```

### ThreadPoolThreadCount

Number of active thread pool threads.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ThreadPoolThreadCount { get; init; }
```

### Timestamp

Elapsed time since the trace started.

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

```csharp
public TimeSpan Timestamp { get; init; }
```

### WorkingSetMb

Process working set in megabytes.

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double WorkingSetMb { get; init; }
```

## Methods

### Deconstruct(out TimeSpan, out double, out double, out double, out long, out long, out long, out int, out long, out long, out long)

**Parameters:**

- `Timestamp` ([TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan))
- `CpuUsagePercent` ([Double](https://learn.microsoft.com/dotnet/api/system.double))
- `WorkingSetMb` ([Double](https://learn.microsoft.com/dotnet/api/system.double))
- `GcHeapSizeMb` ([Double](https://learn.microsoft.com/dotnet/api/system.double))
- `Gen0Collections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `Gen1Collections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `Gen2Collections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `ThreadPoolThreadCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ThreadPoolQueueLength` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `ExceptionCount` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `ActiveTimerCount` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public void Deconstruct(out TimeSpan Timestamp, out double CpuUsagePercent, out double WorkingSetMb, out double GcHeapSizeMb, out long Gen0Collections, out long Gen1Collections, out long Gen2Collections, out int ThreadPoolThreadCount, out long ThreadPoolQueueLength, out long ExceptionCount, out long ActiveTimerCount)
```

### Equals(CounterSnapshot?)

**Parameters:**

- `other` ([CounterSnapshot](/api/dotsider.core.analysis.models.countersnapshot/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(CounterSnapshot? other)
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

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(CounterSnapshot?, CounterSnapshot?)

**Parameters:**

- `left` ([CounterSnapshot](/api/dotsider.core.analysis.models.countersnapshot/))
- `right` ([CounterSnapshot](/api/dotsider.core.analysis.models.countersnapshot/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(CounterSnapshot? left, CounterSnapshot? right)
```

### operator ==(CounterSnapshot?, CounterSnapshot?)

**Parameters:**

- `left` ([CounterSnapshot](/api/dotsider.core.analysis.models.countersnapshot/))
- `right` ([CounterSnapshot](/api/dotsider.core.analysis.models.countersnapshot/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(CounterSnapshot? left, CounterSnapshot? right)
```
