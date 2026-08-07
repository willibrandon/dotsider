---
title: "SizeBasisResolver"
description: "Resolves the total-size basis for a comparison of mstat inputs. The rule is shared by the CLI, the MCP server, and the session protocol so a size figure never changes meaning between surfaces: binaries measure file size on disk; a bare .mstat anywhere forces mstat totals for both sides."
slug: api/dotsider.core.analysis.sizebasisresolver
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Resolves the total-size basis for a comparison of mstat inputs. The rule is shared by the
CLI, the MCP server, and the session protocol so a size figure never changes meaning
between surfaces: binaries measure file size on disk; a bare `.mstat` anywhere forces
mstat totals for both sides.

```csharp
public static class SizeBasisResolver
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeBasisResolver**

## Methods

### Resolve(MstatSource, MstatSource?, MstatDiffResult)

Resolves the basis and totals for a target and optional baseline.

**Parameters:**

- `target` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/)): The build under check.
- `baseline` ([MstatSource](/api/dotsider.core.analysis.models.mstatsource/)): The baseline, or null when the check runs without one.
- `diff` ([MstatDiffResult](/api/dotsider.core.analysis.models.mstatdiffresult/)): The computed size diff, whose summary carries the mstat totals.

**Returns:** [SizeTotals](/api/dotsider.core.analysis.models.sizetotals/)

The shared-basis totals.

```csharp
public static SizeTotals Resolve(MstatSource target, MstatSource? baseline, MstatDiffResult diff)
```
