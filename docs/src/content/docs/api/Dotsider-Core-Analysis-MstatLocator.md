---
title: "MstatLocator"
description: "Resolves a size-comparison input to its mstat report. A bare .mstat file is read directly (detected by extension or by String) — an mstat is itself a valid ECMA-335 assembly, so probing must come before any managed-assembly interpretation); a Native AOT binary resolves through its sidecar discovery (app.mstat beside the binary, or the ILC intermediate output tree). Anything else — a managed assembly, a native binary without a size report — resolves to null."
slug: api/dotsider.core.analysis.mstatlocator
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Resolves a size-comparison input to its mstat report. A bare `.mstat` file is read
directly (detected by extension or by [String)](/api/dotsider.core.analysis.mstatreader.probe(system.string)/) — an mstat is
itself a valid ECMA-335 assembly, so probing must come before any managed-assembly
interpretation); a Native AOT binary resolves through its sidecar discovery
(`app.mstat` beside the binary, or the ILC intermediate output tree). Anything else —
a managed assembly, a native binary without a size report — resolves to null.

```csharp
public static class MstatLocator
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatLocator**

## Methods

### Resolve(string)

Resolves a file to its mstat report, or null when the file is not mstat-backed.

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A `.mstat` file or a Native AOT binary.

**Returns:** [MstatSource](/api/dotsider.core.analysis.models.mstatsource/)

The resolved source, or null.

```csharp
public static MstatSource? Resolve(string filePath)
```

