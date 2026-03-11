---
title: "TraceEventEntry"
description: "A single traced runtime event captured from the EventPipe session."
slug: api/dotsider.core.analysis.models.traceevententry
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A single traced runtime event captured from the EventPipe session.

```csharp
public sealed record TraceEventEntry : IEquatable<TraceEventEntry>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **TraceEventEntry**

## Implements

- [IEquatable\<TraceEventEntry\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### TraceEventEntry(TimeSpan, TraceEventCategory, string, string, int)

A single traced runtime event captured from the EventPipe session.

**Parameters:**

- `Timestamp` ([TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)): 
- `Category` ([TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)): 
- `EventName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `Detail` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 
- `MetadataToken` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): 

```csharp
public TraceEventEntry(TimeSpan Timestamp, TraceEventCategory Category, string EventName, string Detail, int MetadataToken = 0)
```

## Properties

### Category

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
public TraceEventCategory Category { get; init; }
```

### Detail

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Detail { get; init; }
```

### EventName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string EventName { get; init; }
```

### MetadataToken

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MetadataToken { get; init; }
```

### Timestamp

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

```csharp
public TimeSpan Timestamp { get; init; }
```

