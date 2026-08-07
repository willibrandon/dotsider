---
title: "MstatContributorsPayload"
description: "Native AOT size-contributor query results. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.mstatcontributorspayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Native AOT size-contributor query results.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record MstatContributorsPayload : IEquatable<MstatContributorsPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatContributorsPayload**

## Implements

- [IEquatable\<MstatContributorsPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatContributorsPayload(MstatSourceSummaryPayload, MstatFiltersPayload, int, int, bool, bool?, string?, IReadOnlyList\<MstatContributorPayload\>)

Native AOT size-contributor query results.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Source` ([MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/))
- `Filters` ([MstatFiltersPayload](/api/dotsider.core.protocol.mstatfilterspayload/))
- `TotalMatches` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Returned` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Truncated` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `WhyAvailable` ([Nullable\<Boolean\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `WhyUnavailableReason` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Contributors` ([IReadOnlyList\<MstatContributorPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public MstatContributorsPayload(MstatSourceSummaryPayload Source, MstatFiltersPayload Filters, int TotalMatches, int Returned, bool Truncated, bool? WhyAvailable, string? WhyUnavailableReason, IReadOnlyList<MstatContributorPayload> Contributors)
```

## Properties

### Contributors

**Returns:** [IReadOnlyList\<MstatContributorPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MstatContributorPayload> Contributors { get; init; }
```

### Filters

**Returns:** [MstatFiltersPayload](/api/dotsider.core.protocol.mstatfilterspayload/)

```csharp
public MstatFiltersPayload Filters { get; init; }
```

### Returned

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Returned { get; init; }
```

### Source

**Returns:** [MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/)

```csharp
public MstatSourceSummaryPayload Source { get; init; }
```

### TotalMatches

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TotalMatches { get; init; }
```

### Truncated

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Truncated { get; init; }
```

### WhyAvailable

**Returns:** [Nullable\<Boolean\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public bool? WhyAvailable { get; init; }
```

### WhyUnavailableReason

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? WhyUnavailableReason { get; init; }
```

## Methods

### Deconstruct(out MstatSourceSummaryPayload, out MstatFiltersPayload, out int, out int, out bool, out bool?, out string?, out IReadOnlyList\<MstatContributorPayload\>)

**Parameters:**

- `Source` ([MstatSourceSummaryPayload](/api/dotsider.core.protocol.mstatsourcesummarypayload/))
- `Filters` ([MstatFiltersPayload](/api/dotsider.core.protocol.mstatfilterspayload/))
- `TotalMatches` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Returned` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Truncated` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `WhyAvailable` ([Nullable\<Boolean\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `WhyUnavailableReason` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Contributors` ([IReadOnlyList\<MstatContributorPayload\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out MstatSourceSummaryPayload Source, out MstatFiltersPayload Filters, out int TotalMatches, out int Returned, out bool Truncated, out bool? WhyAvailable, out string? WhyUnavailableReason, out IReadOnlyList<MstatContributorPayload> Contributors)
```

### Equals(MstatContributorsPayload?)

**Parameters:**

- `other` ([MstatContributorsPayload](/api/dotsider.core.protocol.mstatcontributorspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(MstatContributorsPayload? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
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

### operator !=(MstatContributorsPayload?, MstatContributorsPayload?)

**Parameters:**

- `left` ([MstatContributorsPayload](/api/dotsider.core.protocol.mstatcontributorspayload/))
- `right` ([MstatContributorsPayload](/api/dotsider.core.protocol.mstatcontributorspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(MstatContributorsPayload? left, MstatContributorsPayload? right)
```

### operator ==(MstatContributorsPayload?, MstatContributorsPayload?)

**Parameters:**

- `left` ([MstatContributorsPayload](/api/dotsider.core.protocol.mstatcontributorspayload/))
- `right` ([MstatContributorsPayload](/api/dotsider.core.protocol.mstatcontributorspayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(MstatContributorsPayload? left, MstatContributorsPayload? right)
```
