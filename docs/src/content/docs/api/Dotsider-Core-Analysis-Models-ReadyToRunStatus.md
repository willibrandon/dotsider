---
title: "ReadyToRunStatus"
description: "The outcome of probing a PE image for a crossgen2 ReadyToRun header. This is a parse status, not a coverage measure — whether not every method is precompiled is IsPartialImage (the READYTORUN_FLAG_PARTIAL flag), not a status value here."
slug: api/dotsider.core.analysis.models.readytorunstatus
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The outcome of probing a PE image for a crossgen2 ReadyToRun header. This is a parse status,
not a coverage measure — whether not every method is precompiled is
[IsPartialImage](/api/dotsider.core.analysis.models.readytoruninfo.ispartialimage/) (the `READYTORUN_FLAG_PARTIAL` flag), not a
status value here.

```csharp
public enum ReadyToRunStatus
```

## Fields

### Corrupt

A recognized ReadyToRun signature whose header or tables are malformed; surfaced, not usable.

**Returns:** [ReadyToRunStatus](/api/dotsider.core.analysis.models.readytorunstatus/)

```csharp
Corrupt = 2
```

### NotReadyToRun

No ReadyToRun header was found — a plain managed, Native AOT, or native image.

**Returns:** [ReadyToRunStatus](/api/dotsider.core.analysis.models.readytorunstatus/)

```csharp
NotReadyToRun = 0
```

### UnrecognizedNativeHeader

A managed native header directory is present but does not carry the ReadyToRun signature
(for example a legacy NGen image). The binary stays classified as managed, but the
diagnostic is surfaced rather than hidden.

**Returns:** [ReadyToRunStatus](/api/dotsider.core.analysis.models.readytorunstatus/)

```csharp
UnrecognizedNativeHeader = 4
```

### UnsupportedVersion

A recognized ReadyToRun signature with a major version outside the supported range.

**Returns:** [ReadyToRunStatus](/api/dotsider.core.analysis.models.readytorunstatus/)

```csharp
UnsupportedVersion = 3
```

### Valid

A valid ReadyToRun header whose section tables parsed successfully.

**Returns:** [ReadyToRunStatus](/api/dotsider.core.analysis.models.readytorunstatus/)

```csharp
Valid = 1
```
