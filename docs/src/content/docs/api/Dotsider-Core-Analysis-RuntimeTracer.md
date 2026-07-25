---
title: "RuntimeTracer"
description: "Manages launching a .NET assembly as a child process and collecting runtime events via EventPipe diagnostics (PID-based connect with retry)."
slug: api/dotsider.core.analysis.runtimetracer
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Manages launching a .NET assembly as a child process and collecting
runtime events via EventPipe diagnostics (PID-based connect with retry).

```csharp
public sealed class RuntimeTracer : IDisposable
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **RuntimeTracer**

## Implements

- [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

## Constructors

### RuntimeTracer(string, IReadOnlyList\<string\>, Action)

Manages launching a .NET assembly as a child process and collecting
runtime events via EventPipe diagnostics (PID-based connect with retry).

**Parameters:**

- `assemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The managed DLL or executable apphost to launch.
- `arguments` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The literal application arguments to pass to the launched process.
- `invalidate` ([Action](https://learn.microsoft.com/dotnet/api/system.action)): The callback that requests a UI refresh.

```csharp
public RuntimeTracer(string assemblyPath, IReadOnlyList<string> arguments, Action invalidate)
```

## Properties

### Elapsed

The elapsed time since the trace was started.

**Returns:** [TimeSpan](https://learn.microsoft.com/dotnet/api/system.timespan)

```csharp
public TimeSpan Elapsed { get; }
```

### ErrorMessage

The error message if the trace failed, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ErrorMessage { get; }
```

### ExitCode

The exit code of the traced process, or null if not yet exited.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? ExitCode { get; }
```

### ProcessId

The OS process ID of the traced process, or null if not started.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? ProcessId { get; }
```

### ProcessState

The current state of the traced process.

**Returns:** [TraceProcessState](/api/dotsider.core.analysis.models.traceprocessstate/)

```csharp
public TraceProcessState ProcessState { get; }
```

## Methods

### Dispose()

Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.

```csharp
public void Dispose()
```

### GetEvents()

Returns a snapshot of all collected events (copied under lock).

**Returns:** [IReadOnlyList\<TraceEventEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<TraceEventEntry> GetEvents()
```

### GetLatestCounters()

Returns the most recent counter snapshot, or null.

**Returns:** [CounterSnapshot](/api/dotsider.core.analysis.models.countersnapshot/)

```csharp
public CounterSnapshot? GetLatestCounters()
```

### GetOutput()

Returns process output lines.

**Returns:** [IReadOnlyList\<OutputLine\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<OutputLine> GetOutput()
```

### GetSummary()

Returns aggregated summary statistics.

**Returns:** [TraceSummary](/api/dotsider.core.analysis.models.tracesummary/)

```csharp
public TraceSummary GetSummary()
```

### Start()

Launches the target process and starts collecting events.

```csharp
public void Start()
```

### Stop()

Stops the traced process and event collection.

```csharp
public void Stop()
```

