---
title: "OutputLine"
description: "A line of output captured from the traced process's stdout or stderr."
slug: api/dotsider.core.analysis.models.outputline
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A line of output captured from the traced process's stdout or stderr.

```csharp
public sealed record OutputLine : IEquatable<OutputLine>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **OutputLine**

## Implements

- [IEquatable\<OutputLine\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### OutputLine(TimeSpan, bool, string)

A line of output captured from the traced process's stdout or stderr.

**Parameters:**

- `Timestamp` ([TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)): Elapsed time since the trace started.
- `IsStdErr` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the line came from stderr rather than stdout.
- `Text` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The captured text content.

```csharp
public OutputLine(TimeSpan Timestamp, bool IsStdErr, string Text)
```

## Properties

### IsStdErr

Whether the line came from stderr rather than stdout.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsStdErr { get; init; }
```

### Text

The captured text content.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Text { get; init; }
```

### Timestamp

Elapsed time since the trace started.

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

```csharp
public TimeSpan Timestamp { get; init; }
```

## Methods

### Deconstruct(out TimeSpan, out bool, out string)

**Parameters:**

- `Timestamp` ([TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan))
- `IsStdErr` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `Text` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out TimeSpan Timestamp, out bool IsStdErr, out string Text)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(OutputLine?)

**Parameters:**

- `other` ([OutputLine](/api/dotsider.core.analysis.models.outputline/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(OutputLine? other)
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

### operator !=(OutputLine?, OutputLine?)

**Parameters:**

- `left` ([OutputLine](/api/dotsider.core.analysis.models.outputline/))
- `right` ([OutputLine](/api/dotsider.core.analysis.models.outputline/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(OutputLine? left, OutputLine? right)
```

### operator ==(OutputLine?, OutputLine?)

**Parameters:**

- `left` ([OutputLine](/api/dotsider.core.analysis.models.outputline/))
- `right` ([OutputLine](/api/dotsider.core.analysis.models.outputline/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(OutputLine? left, OutputLine? right)
```
