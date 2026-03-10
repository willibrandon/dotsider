---
title: "CounterSnapshot"
description: "A snapshot of runtime performance counters at a point in time."
slug: api/dotsider.core.analysis.models.countersnapshot
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

- `Timestamp` ([TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)): 
- `CpuUsagePercent` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 
- `WorkingSetMb` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 
- `GcHeapSizeMb` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): 
- `Gen0Collections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 
- `Gen1Collections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 
- `Gen2Collections` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 
- `ThreadPoolThreadCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 
- `ThreadPoolQueueLength` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 
- `ExceptionCount` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 
- `ActiveTimerCount` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): 

```csharp
public CounterSnapshot(TimeSpan Timestamp, double CpuUsagePercent, double WorkingSetMb, double GcHeapSizeMb, long Gen0Collections, long Gen1Collections, long Gen2Collections, int ThreadPoolThreadCount, long ThreadPoolQueueLength, long ExceptionCount, long ActiveTimerCount)
```

## Properties

### ActiveTimerCount

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long ActiveTimerCount { get; init; }
```

### CpuUsagePercent

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double CpuUsagePercent { get; init; }
```

### ExceptionCount

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long ExceptionCount { get; init; }
```

### GcHeapSizeMb

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double GcHeapSizeMb { get; init; }
```

### Gen0Collections

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Gen0Collections { get; init; }
```

### Gen1Collections

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Gen1Collections { get; init; }
```

### Gen2Collections

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Gen2Collections { get; init; }
```

### ThreadPoolQueueLength

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long ThreadPoolQueueLength { get; init; }
```

### ThreadPoolThreadCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ThreadPoolThreadCount { get; init; }
```

### Timestamp

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

```csharp
public TimeSpan Timestamp { get; init; }
```

### WorkingSetMb

**Returns:** [Double](https://learn.microsoft.com/dotnet/api/system.double)

```csharp
public double WorkingSetMb { get; init; }
```

