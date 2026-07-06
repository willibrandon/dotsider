---
title: "MstatDiffer"
description: "Compares two ILC size reports and explains where the bytes went: a hierarchical delta tree (assembly → namespace → type → method, beside the binary's data categories), flat top contributors, and per-assembly / per-namespace aggregate deltas. Entries are matched by the build-stable identity keys of MstatSizeIndex, so overloads, folded MethodTables, and owner-grouped frozen objects compare correctly across builds."
slug: api/dotsider.core.analysis.mstatdiffer
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Compares two ILC size reports and explains where the bytes went: a hierarchical delta tree
(assembly → namespace → type → method, beside the binary's data categories), flat top
contributors, and per-assembly / per-namespace aggregate deltas. Entries are matched by the
build-stable identity keys of [MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/), so overloads, folded
MethodTables, and owner-grouped frozen objects compare correctly across builds.

```csharp
public static class MstatDiffer
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatDiffer**

## Methods

### Compare(MstatData, MstatData)

Compares two decoded reports under a shared detail-section policy
(MstatData)), so mixed format versions degrade to blob
fidelity together and no byte is counted differently on the two sides.

**Parameters:**

- `left` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/)): The baseline report. Use [Empty](/api/dotsider.core.analysis.models.mstatdata.empty/) when there is none.
- `right` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/)): The report under comparison.

**Returns:** [MstatDiffResult](/api/dotsider.core.analysis.models.mstatdiffresult/)

The size difference.

```csharp
public static MstatDiffResult Compare(MstatData left, MstatData right)
```

### Compare(MstatSizeIndex, MstatSizeIndex)

Compares two normalized indexes. Both must have been created under the same
[MstatSectionPolicy](/api/dotsider.core.analysis.models.mstatsectionpolicy/) — otherwise the same bytes sit in different sections
on the two sides and the comparison is meaningless.

**Parameters:**

- `left` ([MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/)): The baseline index.
- `right` ([MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/)): The index under comparison.

**Returns:** [MstatDiffResult](/api/dotsider.core.analysis.models.mstatdiffresult/)

The size difference.

```csharp
public static MstatDiffResult Compare(MstatSizeIndex left, MstatSizeIndex right)
```

