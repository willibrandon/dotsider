---
title: "TraceEventEntry"
description: "A single traced runtime event captured from the EventPipe session."
slug: api/dotsider.core.analysis.models.traceevententry
sidebar:
  order: 2
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

- `Timestamp` ([TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)): Elapsed time since the trace started.
- `Category` ([TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)): The event category (JIT, GC, Loader, etc.).
- `EventName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Name of the event (e.g., `MethodJittingStarted`).
- `Detail` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Human-readable description of the event payload.
- `MetadataToken` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Metadata token associated with the event, or 0 if not applicable.

```csharp
public TraceEventEntry(TimeSpan Timestamp, TraceEventCategory Category, string EventName, string Detail, int MetadataToken = 0)
```

## Properties

### Category

The event category (JIT, GC, Loader, etc.).

**Returns:** [TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/)

```csharp
public TraceEventCategory Category { get; init; }
```

### Detail

Human-readable description of the event payload.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Detail { get; init; }
```

### EventName

Name of the event (e.g., `MethodJittingStarted`).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string EventName { get; init; }
```

### MetadataToken

Metadata token associated with the event, or 0 if not applicable.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MetadataToken { get; init; }
```

### Timestamp

Elapsed time since the trace started.

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

```csharp
public TimeSpan Timestamp { get; init; }
```

## Methods

### Deconstruct(out TimeSpan, out TraceEventCategory, out string, out string, out int)

**Parameters:**

- `Timestamp` ([TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan))
- `Category` ([TraceEventCategory](/api/dotsider.core.analysis.models.traceeventcategory/))
- `EventName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Detail` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `MetadataToken` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out TimeSpan Timestamp, out TraceEventCategory Category, out string EventName, out string Detail, out int MetadataToken)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(TraceEventEntry?)

**Parameters:**

- `other` ([TraceEventEntry](/api/dotsider.core.analysis.models.traceevententry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(TraceEventEntry? other)
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

### operator !=(TraceEventEntry?, TraceEventEntry?)

**Parameters:**

- `left` ([TraceEventEntry](/api/dotsider.core.analysis.models.traceevententry/))
- `right` ([TraceEventEntry](/api/dotsider.core.analysis.models.traceevententry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(TraceEventEntry? left, TraceEventEntry? right)
```

### operator ==(TraceEventEntry?, TraceEventEntry?)

**Parameters:**

- `left` ([TraceEventEntry](/api/dotsider.core.analysis.models.traceevententry/))
- `right` ([TraceEventEntry](/api/dotsider.core.analysis.models.traceevententry/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(TraceEventEntry? left, TraceEventEntry? right)
```
