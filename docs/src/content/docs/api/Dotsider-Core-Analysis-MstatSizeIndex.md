---
title: "MstatSizeIndex"
description: "The normalized view of an ILC size report that every size consumer shares: raw rows aggregated under build-stable identity keys, one double-count policy for the 2.1+ detail sections, owner-based attribution for frozen objects, and per-assembly / per-namespace byte totals. SizeAnalyzer builds the Size Map from it, MstatDiffer compares two of them, and budget evaluation reads its aggregates — so a total shown in one place always equals the same total shown in another."
slug: api/dotsider.core.analysis.mstatsizeindex
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

The normalized view of an ILC size report that every size consumer shares: raw rows
aggregated under build-stable identity keys, one double-count policy for the 2.1+ detail
sections, owner-based attribution for frozen objects, and per-assembly / per-namespace byte
totals. [SizeAnalyzer](/api/dotsider.core.analysis.sizeanalyzer/) builds the Size Map from it, [MstatDiffer](/api/dotsider.core.analysis.mstatdiffer/)
compares two of them, and budget evaluation reads its aggregates — so a total shown in one
place always equals the same total shown in another.

```csharp
public sealed class MstatSizeIndex
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatSizeIndex**

## Properties

### AssemblyTotals

Attributable bytes per assembly: methods, MethodTables, RVA fields, and resources by
their defining assembly, frozen objects by their owning type's assembly (ownerless
bytes land under [UnattributedName](/api/dotsider.core.analysis.mstatsizeindex.unattributedname/)). Blobs are global and excluded.

**Returns:** [IReadOnlyDictionary\<String, Int64\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

```csharp
public IReadOnlyDictionary<string, long> AssemblyTotals { get; }
```

### Data

The decoded report the index was built from.

**Returns:** [MstatData](/api/dotsider.core.analysis.models.mstatdata/)

```csharp
public MstatData Data { get; }
```

### Entries

Every normalized entry, in first-occurrence order per section.

**Returns:** [IReadOnlyList\<MstatSizeEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatSizeEntry> Entries { get; }
```

### NamespaceTotals

Attributable bytes per namespace, folded across assemblies: methods and MethodTables by
their namespace, RVA fields by their declaring type's namespace, frozen objects by
their owning type's namespace (ownerless bytes land under
[UnattributedName](/api/dotsider.core.analysis.mstatsizeindex.unattributedname/)). Blobs and resources carry no namespace and are
excluded. The global namespace keys as an empty string.

**Returns:** [IReadOnlyDictionary\<String, Int64\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

```csharp
public IReadOnlyDictionary<string, long> NamespaceTotals { get; }
```

### Policy

The detail-section policy the index applied.

**Returns:** [MstatSectionPolicy](/api/dotsider.core.analysis.models.mstatsectionpolicy/)

```csharp
public MstatSectionPolicy Policy { get; }
```

### Total

The total attributable bytes — the same figure the Size Map reports for the build.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Total { get; }
```

## Methods

### Create(MstatData, MstatSectionPolicy)

Builds the index under an explicit detail-section policy. Two indexes are comparable by
[MstatDiffer](/api/dotsider.core.analysis.mstatdiffer/) only when they share a policy — use
MstatData) for a pair of reports.

**Parameters:**

- `data` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/)): The decoded report.
- `policy` ([MstatSectionPolicy](/api/dotsider.core.analysis.models.mstatsectionpolicy/)): The detail-section policy to apply.

**Returns:** [MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/)

The normalized index.

```csharp
public static MstatSizeIndex Create(MstatData data, MstatSectionPolicy policy)
```

### Create(MstatData)

Builds the index for one report on its own, using [MstatData)](/api/dotsider.core.analysis.models.mstatsectionpolicy.fordata(dotsider.core.analysis.models.mstatdata)/).

**Parameters:**

- `data` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/)): The decoded report.

**Returns:** [MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/)

The normalized index.

```csharp
public static MstatSizeIndex Create(MstatData data)
```

## Fields

### UnattributedName

The attribution bucket for bytes no assembly or namespace can honestly be charged for —
frozen objects with no owning type, such as string literals. Scoped size budgets never
draw from this bucket, but it stays visible in the aggregates so the bytes are never
silently dropped.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public const string UnattributedName = "(unattributed)"
```
