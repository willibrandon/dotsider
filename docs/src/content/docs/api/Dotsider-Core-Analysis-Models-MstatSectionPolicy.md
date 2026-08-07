---
title: "MstatSectionPolicy"
description: "Decides which of an mstat's 2.1+ detail sections carry the bytes that the format double-reports into blob buckets. Every 2.x report sums frozen object, field RVA, and resource bytes into the ArrayOfFrozenObjects, FieldRvaData, and ResourceData blobs for back-compat; a reader must pick, per section, either the detail entries or the bucket blob — never both. Sharing this policy between SizeAnalyzer, MstatSizeIndex, and MstatDiffer is what keeps their totals identical."
slug: api/dotsider.core.analysis.models.mstatsectionpolicy
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Decides which of an mstat's 2.1+ detail sections carry the bytes that the format
double-reports into blob buckets. Every 2.x report sums frozen object, field RVA, and
resource bytes into the `ArrayOfFrozenObjects`, `FieldRvaData`, and
`ResourceData` blobs for back-compat; a reader must pick, per section, either the
detail entries or the bucket blob — never both. Sharing this policy between
[SizeAnalyzer](/api/dotsider.core.analysis.sizeanalyzer/), [MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/),
and [MstatDiffer](/api/dotsider.core.analysis.mstatdiffer/) is what keeps their totals identical.

```csharp
public readonly struct MstatSectionPolicy : IEquatable<MstatSectionPolicy>
```

## Implements

- [IEquatable\<MstatSectionPolicy\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatSectionPolicy(bool, bool, bool)

Decides which of an mstat's 2.1+ detail sections carry the bytes that the format
double-reports into blob buckets. Every 2.x report sums frozen object, field RVA, and
resource bytes into the `ArrayOfFrozenObjects`, `FieldRvaData`, and
`ResourceData` blobs for back-compat; a reader must pick, per section, either the
detail entries or the bucket blob — never both. Sharing this policy between
[SizeAnalyzer](/api/dotsider.core.analysis.sizeanalyzer/), [MstatSizeIndex](/api/dotsider.core.analysis.mstatsizeindex/),
and [MstatDiffer](/api/dotsider.core.analysis.mstatdiffer/) is what keeps their totals identical.

**Parameters:**

- `UseFrozenObjects` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): True to take frozen objects from the detail section and exclude the `ArrayOfFrozenObjects` blob.
- `UseRvaFields` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): True to take field RVA data from the detail section and exclude the `FieldRvaData` blob.
- `UseManifestResources` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): True to take resources from the detail section and exclude the `ResourceData` blob.

```csharp
public MstatSectionPolicy(bool UseFrozenObjects, bool UseRvaFields, bool UseManifestResources)
```

## Properties

### UseFrozenObjects

True to take frozen objects from the detail section and exclude the `ArrayOfFrozenObjects` blob.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool UseFrozenObjects { get; init; }
```

### UseManifestResources

True to take resources from the detail section and exclude the `ResourceData` blob.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool UseManifestResources { get; init; }
```

### UseRvaFields

True to take field RVA data from the detail section and exclude the `FieldRvaData` blob.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool UseRvaFields { get; init; }
```

## Methods

### Deconstruct(out bool, out bool, out bool)

**Parameters:**

- `UseFrozenObjects` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `UseRvaFields` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `UseManifestResources` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))

```csharp
public void Deconstruct(out bool UseFrozenObjects, out bool UseRvaFields, out bool UseManifestResources)
```

### Equals(MstatSectionPolicy)

**Parameters:**

- `other` ([MstatSectionPolicy](/api/dotsider.core.analysis.models.mstatsectionpolicy/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatSectionPolicy other)
```

### Equals(object)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object obj)
```

### ExcludedBlobNames()

The blob names this policy excludes — the buckets whose bytes are read from a detail
section instead.

**Returns:** [IReadOnlySet\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlyset-1)

The excluded blob names.

```csharp
public IReadOnlySet<string> ExcludedBlobNames()
```

### ForData(MstatData)

The policy for reading one report on its own: each detail section is used when it has
entries. A 1.x report has empty detail sections, so everything stays at blob fidelity.

**Parameters:**

- `data` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/)): The report to derive the policy from.

**Returns:** [MstatSectionPolicy](/api/dotsider.core.analysis.models.mstatsectionpolicy/)

The single-report policy.

```csharp
public static MstatSectionPolicy ForData(MstatData data)
```

### ForPair(MstatData, MstatData)

The policy for comparing two reports, applied to both sides so the same bytes land in
the same section everywhere. A detail section is used only when every non-empty side
understands it (format 2.1+) and at least one side has entries; otherwise both sides
degrade to blob fidelity for that section, which loses no bytes because 2.x
double-reports them. [Empty](/api/dotsider.core.analysis.models.mstatdata.empty/) (format 0.0) is transparent: it
constrains nothing.

**Parameters:**

- `left` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/)): The baseline report.
- `right` ([MstatData](/api/dotsider.core.analysis.models.mstatdata/)): The report under comparison.

**Returns:** [MstatSectionPolicy](/api/dotsider.core.analysis.models.mstatsectionpolicy/)

The shared policy for both sides.

```csharp
public static MstatSectionPolicy ForPair(MstatData left, MstatData right)
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

### operator !=(MstatSectionPolicy, MstatSectionPolicy)

**Parameters:**

- `left` ([MstatSectionPolicy](/api/dotsider.core.analysis.models.mstatsectionpolicy/))
- `right` ([MstatSectionPolicy](/api/dotsider.core.analysis.models.mstatsectionpolicy/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatSectionPolicy left, MstatSectionPolicy right)
```

### operator ==(MstatSectionPolicy, MstatSectionPolicy)

**Parameters:**

- `left` ([MstatSectionPolicy](/api/dotsider.core.analysis.models.mstatsectionpolicy/))
- `right` ([MstatSectionPolicy](/api/dotsider.core.analysis.models.mstatsectionpolicy/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatSectionPolicy left, MstatSectionPolicy right)
```
