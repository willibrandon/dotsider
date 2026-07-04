---
title: "TraceProcessState"
description: "Current state of the traced process lifecycle."
slug: api/dotsider.core.analysis.models.traceprocessstate
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Current state of the traced process lifecycle.

```csharp
public enum TraceProcessState
```

## Fields

### Error

The trace session encountered an error.

**Returns:** [TraceProcessState](/api/dotsider.core.analysis.models.traceprocessstate/)

```csharp
Error = 4
```

### Exited

The traced process has terminated normally.

**Returns:** [TraceProcessState](/api/dotsider.core.analysis.models.traceprocessstate/)

```csharp
Exited = 3
```

### Idle

No process is being traced.

**Returns:** [TraceProcessState](/api/dotsider.core.analysis.models.traceprocessstate/)

```csharp
Idle = 0
```

### Running

The trace session is actively collecting events from the process.

**Returns:** [TraceProcessState](/api/dotsider.core.analysis.models.traceprocessstate/)

```csharp
Running = 2
```

### Starting

The trace session is initializing and attaching to the process.

**Returns:** [TraceProcessState](/api/dotsider.core.analysis.models.traceprocessstate/)

```csharp
Starting = 1
```

