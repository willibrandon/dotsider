---
title: "TraceEventCategory"
description: "Category of a traced runtime event, used for coloring in the events table."
slug: api/dotsider.core.analysis.models.traceeventcategory
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Category of a traced runtime event, used for coloring in the events table.

```csharp
public enum TraceEventCategory
```

## Fields

### Counter

Runtime performance counter snapshots.

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
Counter = 7
```

### Exception

Exception throw and catch events.

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
Exception = 2
```

### GC

Garbage collection events.

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
GC = 0
```

### Http

HTTP request and response events.

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
Http = 5
```

### JIT

Just-in-time compilation events.

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
JIT = 1
```

### Loader

Assembly and module loader events.

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
Loader = 3
```

### Other

Events that do not fit any other category.

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
Other = 8
```

### Socket

Socket-level network I/O events.

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
Socket = 6
```

### Threading

Thread pool and synchronization events.

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
Threading = 4
```
